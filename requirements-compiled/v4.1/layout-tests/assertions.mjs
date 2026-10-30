// Executable assertions for the Forma layout contract (analysis/forma-layout-contract.json, rules FL-01..FL-10).
//
// Each assertion takes a Playwright page (and, where needed, options) and returns the list of violations it found:
// an empty list is a pass. The same functions run in two places:
//   - in this package's CI, against Forma 0.12.0's own pattern pages (each must pass) and against a deliberately
//     broken fixture per assertion (each must fail), so every assertion is shown to be executable and not vacuous;
//   - in the event build, against Causa's screens, from the plan-bound browser tests the rule names.
// Assertions only read the page; none changes it except FL-04 and FL-09, which press keys as a user would.

const inPage = (page, fn, arg) => page.evaluate(fn, arg);

// FL-01: every interactive control is at least 44x44 CSS px (--ef-layout-target-min).
export const touchTargets = page =>
  inPage(page, () => {
    const describe = e => `${e.tagName.toLowerCase()}${e.className && typeof e.className === 'string' ? '.' + e.className.trim().split(/\s+/).join('.') : ''} "${(e.textContent || '').trim().slice(0, 30)}"`;
    const selector = 'button, a[href], input:not([type=hidden]), select, textarea, summary, [role=button], [role=tab], [role=menuitem], [role=checkbox], [role=switch]';
    const visible = e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(e).visibility !== 'hidden'; };
    return [...document.querySelectorAll(selector)]
      .filter(visible)
      .map(e => [e, e.getBoundingClientRect()])
      .filter(([, r]) => r.width < 43.5 || r.height < 43.5)
      .map(([e, r]) => `${describe(e)} is ${Math.round(r.width)}x${Math.round(r.height)} px`);
  });

// FL-02: the page never scrolls horizontally.
export const noHorizontalScroll = page =>
  inPage(page, () => {
    const width = document.scrollingElement.scrollWidth;
    return width > window.innerWidth + 0.5 ? [`page is ${width} px wide in a ${window.innerWidth} px viewport`] : [];
  });

// FL-03: text is never clipped without an accessible full value.
export const textNotClipped = page =>
  inPage(page, () => {
    const clips = s => ['hidden', 'clip'].includes(s.overflowX) || s.textOverflow === 'ellipsis';
    const hasText = e => [...e.childNodes].some(n => n.nodeType === 3 && n.textContent.trim() !== '');
    const labelled = e => e.hasAttribute('title') || e.hasAttribute('aria-label') || e.hasAttribute('aria-describedby');
    return [...document.body.querySelectorAll('*')]
      .filter(e => hasText(e) && clips(getComputedStyle(e)) && e.scrollWidth > e.clientWidth + 1 && !labelled(e))
      .map(e => `"${e.textContent.trim().slice(0, 40)}" is clipped (${e.scrollWidth} > ${e.clientWidth} px)`);
  });

// FL-04: every focusable element reached by Tab shows a focus ring at least --ef-focus-ring-width wide.
export const focusVisible = async (page, { maxTabs = 25 } = {}) => {
  const ring = await inPage(page, () => {
    const probe = document.createElement('div');
    probe.style.width = 'var(--ef-focus-ring-width, 3px)';
    document.body.appendChild(probe);
    const px = probe.getBoundingClientRect().width;
    probe.remove();
    return px;
  });
  const step = async seen => {
    await page.keyboard.press('Tab');
    return inPage(page, minWidth => {
      const e = document.activeElement;
      if (!e || e === document.body) return null;
      const s = getComputedStyle(e);
      const outline = s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) >= minWidth - 0.5;
      const shadow = s.boxShadow && s.boxShadow !== 'none';
      return { key: e.outerHTML.slice(0, 80), ok: outline || shadow, label: `${e.tagName.toLowerCase()} "${(e.textContent || '').trim().slice(0, 30)}"` };
    }, ring).then(r => (r === null || seen.some(x => x.key === r.key) ? seen : [...seen, r]));
  };
  const visits = await Array.from({ length: maxTabs }).reduce((acc) => acc.then(step), Promise.resolve([]));
  const interactive = await inPage(page, () => document.querySelectorAll('button, a[href], input, select, textarea, summary, [tabindex]').length);
  return [
    ...(interactive > 0 && visits.length === 0 ? ['no element received focus from the keyboard'] : []),
    ...visits.filter(v => !v.ok).map(v => `${v.label} has no visible focus ring of at least ${ring} px`),
  ];
};

// FL-05: with prefers-reduced-motion, no running animation (CSS or script, document.getAnimations) and no transition
// lasts longer than 10 ms. The caller emulates reducedMotion: 'reduce' before loading the page.
export const reducedMotion = page =>
  inPage(page, () => {
    const seconds = v => v.split(',').map(x => x.trim()).map(x => (x.endsWith('ms') ? parseFloat(x) / 1000 : parseFloat(x) || 0));
    const longest = xs => Math.max(0, ...xs);
    const label = e => (e ? `${e.tagName.toLowerCase()}${typeof e.className === 'string' && e.className ? '.' + e.className.trim().split(/\s+/).join('.') : ''}` : 'document');
    const animations = document.getAnimations()
      .map(a => [a, a.effect ? a.effect.getComputedTiming() : { duration: 0 }])
      .filter(([, t]) => (typeof t.duration === 'number' ? t.duration : 0) > 10)
      .map(([a, t]) => `${label(a.effect && a.effect.target)} animates for ${t.duration} ms`);
    const transitions = [...document.querySelectorAll('*')]
      .map(e => [e, getComputedStyle(e)])
      .filter(([, s]) => s.transitionProperty !== 'none' && longest(seconds(s.transitionDuration)) > 0.01)
      .map(([e, s]) => `${label(e)} transitions for ${s.transitionDuration}`);
    return [...animations, ...transitions];
  });

// FL-06: text contrast at least 4.5:1, or 3:1 for large text (24 px, or 18.66 px bold), against the composited
// background. Text over a background image is not judged here (the axe run of the plan test covers it).
export const textContrast = page =>
  inPage(page, () => {
    const rgba = c => (c.match(/[\d.]+/g) || []).map(Number).concat([1]).slice(0, 4);
    const over = ([r, g, b, a], [R, G, B]) => [r * a + R * (1 - a), g * a + G * (1 - a), b * a + B * (1 - a), 1];
    const background = e => {
      const layers = [];
      for (let n = e; n; n = n.parentElement) {
        const s = getComputedStyle(n);
        if (s.backgroundImage !== 'none') return null;
        layers.push(rgba(s.backgroundColor));
      }
      return layers.reverse().reduce((acc, layer) => over(layer, acc), [255, 255, 255, 1]);
    };
    const lum = ([r, g, b]) => [r, g, b].map(v => v / 255).map(v => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)).reduce((acc, v, k) => acc + v * [0.2126, 0.7152, 0.0722][k], 0);
    const ratio = (a, b) => (Math.max(lum(a), lum(b)) + 0.05) / (Math.min(lum(a), lum(b)) + 0.05);
    const hasText = e => [...e.childNodes].some(n => n.nodeType === 3 && n.textContent.trim() !== '');
    const visible = e => { const r = e.getBoundingClientRect(); const s = getComputedStyle(e); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && parseFloat(s.opacity) > 0; };
    return [...document.body.querySelectorAll('*')]
      .filter(e => hasText(e) && visible(e))
      .map(e => [e, getComputedStyle(e), background(e)])
      .filter(([, , bg]) => bg !== null)
      .map(([e, s, bg]) => {
        const size = parseFloat(s.fontSize);
        const large = size >= 24 || (size >= 18.66 && parseInt(s.fontWeight, 10) >= 700);
        const fg = over(rgba(s.color), bg);
        return [e, ratio(fg, bg), large ? 3 : 4.5];
      })
      .filter(([, r, need]) => r < need - 0.005)
      .map(([e, r, need]) => `"${e.textContent.trim().slice(0, 30)}" has contrast ${r.toFixed(2)}:1 (needs ${need}:1)`);
  });

// FL-07: a status indicator is present, carries text (never colour alone) and is visible without scrolling.
export const statusVisible = page =>
  inPage(page, () => {
    const indicators = [...document.querySelectorAll('.ef-status-lozenge, .ef-operation-status, [role=status]')];
    const inView = r => r.width > 0 && r.height > 0 && r.top < window.innerHeight && r.bottom > 0 && r.left < window.innerWidth && r.right > 0;
    return indicators.length === 0
      ? ['no status indicator on the page']
      : indicators.flatMap(e => [
          ...((e.textContent || '').trim() === '' && !e.getAttribute('aria-label') ? [`${e.className} has no text`] : []),
          ...(inView(e.getBoundingClientRect()) ? [] : [`${e.className} is not visible in the initial viewport`]),
        ]);
  });

// FL-08 (loading): a loading placeholder tells assistive technology what is loading.
export const loadingAnnounced = page =>
  inPage(page, () => {
    const loaders = [...document.querySelectorAll('.ef-skeleton, [aria-busy=true], .ef-spinner')];
    const named = e => (e.textContent || '').trim() !== '' || e.getAttribute('aria-label') || e.getAttribute('aria-labelledby') || e.closest('[aria-label],[aria-labelledby]');
    return loaders.length === 0 ? ['no loading placeholder on the page'] : loaders.filter(e => !named(e)).map(e => `${e.className || e.tagName} has no accessible name or text`);
  });

// FL-08 (failure): a fault states what happened in text and offers a next step (a control).
export const faultActionable = page =>
  inPage(page, () => {
    const faults = [...document.querySelectorAll('[class*="ef-fault"], [role=alert]')].filter(e => !e.parentElement.closest('[class*="ef-fault"], [role=alert]'));
    return faults.length === 0
      ? ['no fault on the page']
      : faults.flatMap(e => [
          ...((e.textContent || '').trim().length < 10 ? [`${e.className || 'alert'} does not say what happened`] : []),
          ...(e.querySelector('button, a[href]') ? [] : [`${e.className || 'alert'} offers no next step`]),
        ]);
  });

// FL-09: navigation is reachable by keyboard at this viewport: a visible labelled nav, or a visible disclosure that
// opens one; at most one current page per nav.
export const navigationReachable = async (page, { maxTabs = 15 } = {}) => {
  const state = await inPage(page, () => {
    const visible = e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(e).visibility !== 'hidden'; };
    const navs = [...document.querySelectorAll('nav')];
    return {
      navs: navs.length,
      unlabelled: navs.filter(n => !n.getAttribute('aria-label') && !n.getAttribute('aria-labelledby')).length,
      visible: navs.filter(visible).length,
      disclosure: navs.some(n => n.closest('details') && visible(n.closest('details').querySelector('summary'))),
      currents: navs.map(n => n.querySelectorAll('[aria-current=page]').length).filter(c => c > 1).length,
    };
  });
  if (state.navs === 0) return ['no navigation landmark'];
  const reach = async (n) => {
    if (n === 0) return false;
    await page.keyboard.press('Tab');
    const hit = await inPage(page, () => {
      const e = document.activeElement;
      return !!e && (e.closest('nav') !== null || (e.tagName === 'SUMMARY' && e.parentElement.querySelector('nav') !== null));
    });
    return hit || reach(n - 1);
  };
  const reachable = (state.visible > 0 || state.disclosure) && (await reach(maxTabs));
  return [
    ...(state.unlabelled > 0 ? [`${state.unlabelled} nav landmark(s) without a label`] : []),
    ...(state.currents > 0 ? ['a nav marks more than one current page'] : []),
    ...(reachable ? [] : ['navigation is not reachable by keyboard at this viewport']),
  ];
};

// FL-10: page stylesheets other than Forma's declare no --ef-* property and use only --ef-* properties Forma defines.
export const onlyFormaTokens = (page, { inventory }) =>
  inPage(page, known => {
    const text = [...document.querySelectorAll('style')].map(s => s.textContent).join('\n');
    const declared = [...text.matchAll(/(--ef-[a-z0-9-]+)\s*:/g)].map(m => m[1]);
    const used = [...text.matchAll(/var\(\s*(--ef-[a-z0-9-]+)/g)].map(m => m[1]);
    return [
      ...declared.map(p => `declares ${p}; only Forma defines --ef-* properties`),
      ...used.filter(p => !known.includes(p)).map(p => `uses ${p}, which Forma does not define`),
    ];
  }, inventory);

export const assertions = {
  touchTargets, noHorizontalScroll, textNotClipped, focusVisible, reducedMotion, textContrast,
  statusVisible, loadingAnnounced, faultActionable, navigationReachable, onlyFormaTokens,
};
