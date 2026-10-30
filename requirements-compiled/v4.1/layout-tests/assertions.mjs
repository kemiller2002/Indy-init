// Executable assertions for the Forma layout contract (analysis/forma-layout-contract.json, rules FL-01..FL-10).
//
// Each assertion takes a Playwright page (and options) and returns the list of violations it found: an empty list is
// a pass. The same functions run in two places:
//   - in this package's CI, against Forma 0.12.0's own pattern pages and conforming fixtures (each must pass) and
//     against a deliberately broken fixture per assertion (each must fail), so every assertion is shown to work;
//   - in the event build, where the bootstrap copies this file to tests/browser/support/layout-assertions.mjs and the
//     plan-bound browser tests each rule names import it and run it against Causa's screens.
// Assertions only read the page, except focus and dialog assertions, which press keys and click as a user would.

const inPage = (page, fn, arg) => page.evaluate(fn, arg);

// ---- FL-01 touch targets -----------------------------------------------------------------------------------------

// Every interactive control is at least 44x44 CSS px (--ef-layout-target-min).
export const touchTargets = page =>
  inPage(page, () => {
    const describe = e => `${e.tagName.toLowerCase()} "${(e.textContent || '').trim().slice(0, 30)}"`;
    const selector = 'button, a[href], input:not([type=hidden]), select, textarea, summary, [role=button], [role=tab], [role=menuitem], [role=checkbox], [role=switch]';
    const visible = e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(e).visibility !== 'hidden'; };
    return [...document.querySelectorAll(selector)]
      .filter(visible)
      .map(e => [e, e.getBoundingClientRect()])
      .filter(([, r]) => r.width < 43.5 || r.height < 43.5)
      .map(([e, r]) => `${describe(e)} is ${Math.round(r.width)}x${Math.round(r.height)} px`);
  });

// ---- FL-02 no horizontal page scroll; wide content scrolls in an accessible region --------------------------------

export const noHorizontalScroll = page =>
  inPage(page, () => {
    const width = document.scrollingElement.scrollWidth;
    return width > window.innerWidth + 0.5 ? [`page is ${width} px wide in a ${window.innerWidth} px viewport`] : [];
  });

// Content wider than its container scrolls inside it, and that container is reachable by keyboard and named, so the
// scrolling is perceivable and operable (the visible affordance of FL-02).
export const scrollRegionsAccessible = page =>
  inPage(page, () => {
    const scrolls = e => { const s = getComputedStyle(e); return ['auto', 'scroll'].includes(s.overflowX) && e.scrollWidth > e.clientWidth + 1; };
    const clipped = e => { const s = getComputedStyle(e); return ['hidden', 'clip'].includes(s.overflowX) && e.scrollWidth > e.clientWidth + 1 && e.children.length > 0; };
    const named = e => e.getAttribute('aria-label') || e.getAttribute('aria-labelledby');
    const all = [...document.body.querySelectorAll('*')];
    return [
      ...all.filter(scrolls).filter(e => !(e.tabIndex >= 0 && named(e))).map(e => `${e.className || e.tagName} scrolls but is not focusable and named`),
      ...all.filter(clipped).filter(e => !e.closest('[aria-hidden=true]')).map(e => `${e.className || e.tagName} hides overflowing content instead of scrolling it`),
    ];
  });

// ---- FL-03 text is never clipped; line length within the measure ------------------------------------------------

export const textNotClipped = page =>
  inPage(page, () => {
    const hasText = e => [...e.childNodes].some(n => n.nodeType === 3 && n.textContent.trim() !== '');
    const labelled = e => e.hasAttribute('title') || e.hasAttribute('aria-label');
    const clipsX = (e, s) => (['hidden', 'clip'].includes(s.overflowX) || s.textOverflow === 'ellipsis') && e.scrollWidth > e.clientWidth + 1;
    const clipsY = (e, s) => ['hidden', 'clip'].includes(s.overflowY) && e.scrollHeight > e.clientHeight + 1;
    const clamped = (e, s) => (s.webkitLineClamp && s.webkitLineClamp !== 'none') && e.scrollHeight > e.clientHeight + 1;
    const hidden = e => e.closest('.ef-visually-hidden, [aria-hidden=true]');
    return [...document.body.querySelectorAll('*')]
      .filter(e => hasText(e) && !labelled(e) && !hidden(e))
      .map(e => [e, getComputedStyle(e)])
      .filter(([e, s]) => clipsX(e, s) || clipsY(e, s) || clamped(e, s))
      .map(([e]) => `"${e.textContent.trim().slice(0, 40)}" is clipped without an accessible full value`);
  });

// Paragraph and list text inside a reading region stays within --ef-layout-measure (72ch).
export const lineLength = page =>
  inPage(page, () => {
    const chWidth = e => {
      const probe = document.createElement('span');
      probe.textContent = '0'.repeat(100);
      probe.style.cssText = 'position:absolute;visibility:hidden;white-space:nowrap';
      e.appendChild(probe);
      const w = probe.getBoundingClientRect().width / 100;
      probe.remove();
      return w;
    };
    return [...document.querySelectorAll('.ef-measure p, .ef-prose p, .ef-prose li, .ef-measure li')]
      .map(e => [e, e.getBoundingClientRect().width / chWidth(e)])
      .filter(([, ch]) => ch > 72.5)
      .map(([e, ch]) => `"${e.textContent.trim().slice(0, 30)}" is ${ch.toFixed(0)}ch wide`);
  });

// ---- FL-04 keyboard: skip link first, DOM order, visible focus, dialog focus -------------------------------------

const ringWidth = page =>
  inPage(page, () => {
    const probe = document.createElement('div');
    probe.style.width = 'var(--ef-focus-ring-width, 3px)';
    document.body.appendChild(probe);
    const px = probe.getBoundingClientRect().width;
    probe.remove();
    return px;
  });

const tabSequence = async (page, maxTabs) => {
  const step = async seen => {
    await page.keyboard.press('Tab');
    const key = await inPage(page, () => {
      const e = document.activeElement;
      if (!e || e === document.body) return null;
      if (!e.dataset.efVisit) e.dataset.efVisit = String(document.querySelectorAll('[data-ef-visit]').length);
      return e.dataset.efVisit;
    });
    return key === null || seen.includes(key) ? seen : [...seen, key];
  };
  return Array.from({ length: maxTabs }).reduce(acc => acc.then(step), Promise.resolve([]));
};

// Every element reached by Tab shows a focus ring it does not show when blurred: a non-transparent outline at least
// --ef-focus-ring-width wide, or a box-shadow ring of at least that spread.
export const focusVisible = async (page, { maxTabs = 25 } = {}) => {
  const ring = await ringWidth(page);
  const visits = await Array.from({ length: maxTabs }).reduce(async (acc) => {
    const seen = await acc;
    await page.keyboard.press('Tab');
    const v = await inPage(page, minWidth => {
      const e = document.activeElement;
      if (!e || e === document.body) return null;
      const transparent = c => /rgba\([^)]*,\s*0\)$/.test(c) || c === 'transparent';
      const ringOf = s => {
        const outline = s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) >= minWidth - 0.5 && !transparent(s.outlineColor);
        const shadows = (s.boxShadow === 'none' ? [] : s.boxShadow.split(/,(?![^(]*\))/))
          .map(x => ({ colour: (x.match(/rgba?\([^)]*\)/) || [''])[0], lengths: (x.replace(/rgba?\([^)]*\)/, '').match(/-?[\d.]+px/g) || []).map(parseFloat) }))
          .filter(x => x.lengths.length >= 4 && x.lengths[3] >= minWidth - 0.5 && !transparent(x.colour));
        return outline || shadows.length > 0;
      };
      const focused = getComputedStyle(e);
      const focusedRing = ringOf(focused);
      const signature = s => `${s.outlineStyle} ${s.outlineWidth} ${s.outlineColor} ${s.boxShadow}`;
      const focusedSignature = signature(focused);
      e.blur();
      const blurredSignature = signature(getComputedStyle(e));
      e.focus({ focusVisible: true });
      return { key: e.outerHTML.slice(0, 80), ok: focusedRing && focusedSignature !== blurredSignature, label: `${e.tagName.toLowerCase()} "${(e.textContent || '').trim().slice(0, 30)}"` };
    }, ring);
    return v === null || seen.some(x => x.key === v.key) ? seen : [...seen, v];
  }, Promise.resolve([]));
  const interactive = await inPage(page, () => document.querySelectorAll('button, a[href], input, select, textarea, summary, [tabindex]').length);
  return [
    ...(interactive > 0 && visits.length === 0 ? ['no element received focus from the keyboard'] : []),
    ...visits.filter(v => !v.ok).map(v => `${v.label} shows no focus ring of at least ${ring} px that it lacks when blurred`),
  ];
};

// Tab follows DOM order: no positive tabindex, and the Tab sequence visits focusable elements in document order.
export const focusOrder = async (page, { maxTabs = 25 } = {}) => {
  const positive = await inPage(page, () => [...document.querySelectorAll('[tabindex]')].filter(e => e.tabIndex > 0).map(e => `${e.tagName.toLowerCase()} has tabindex ${e.tabIndex}`));
  await tabSequence(page, maxTabs);
  const order = await inPage(page, () => {
    const visited = [...document.querySelectorAll('[data-ef-visit]')];
    const byVisit = [...visited].sort((a, b) => Number(a.dataset.efVisit) - Number(b.dataset.efVisit));
    const inDom = visited; // querySelectorAll returns document order
    return byVisit.every((e, k) => e === inDom[k]) ? [] : ['the Tab sequence does not follow document order'];
  });
  return [...positive, ...order];
};

// The first Tab reaches a skip link whose target exists.
export const skipLinkFirst = async page => {
  await page.keyboard.press('Tab');
  return inPage(page, () => {
    const e = document.activeElement;
    const href = e && e.getAttribute && e.getAttribute('href');
    const target = href && href.startsWith('#') ? document.getElementById(href.slice(1)) : null;
    return e && e.tagName === 'A' && /skip/i.test(e.textContent) && target ? [] : ['the first Tab does not reach a skip link with an existing target'];
  });
};

// Opening a dialog moves focus into it, Tab stays inside it, and Escape closes it and returns focus to its invoker.
export const dialogFocus = async page => {
  const invokers = await inPage(page, () =>
    [...document.querySelectorAll('button[commandfor]:not([command=close]):not([command=request-close]), [aria-haspopup=dialog][aria-controls]')]
      .map((b, k) => { b.dataset.efInvoker = String(k); return { key: String(k), dialog: b.getAttribute('commandfor') || b.getAttribute('aria-controls') }; })
      .filter(x => { const d = document.getElementById(x.dialog); return d && d.tagName === 'DIALOG'; }));
  if (invokers.length === 0) return ['no dialog invoker on the page'];
  const open = async (key, dialog) => {
    await page.focus(`[data-ef-invoker="${key}"]`);
    await page.keyboard.press('Enter');
    // Browsers without invoker commands: open it as the command would.
    await inPage(page, id => { const d = document.getElementById(id); if (!d.open) d.showModal(); }, dialog);
  };
  // Focus may leave a native modal dialog for the browser's own interface (activeElement becomes body); it must never
  // land on page content outside the dialog.
  const contained = dialog => inPage(page, id => { const a = document.activeElement; return !a || a === document.body || document.getElementById(id).contains(a); }, dialog);
  const check = async ({ key, dialog }) => {
    await open(key, dialog);
    const opened = await inPage(page, id => document.getElementById(id).contains(document.activeElement), dialog);
    await page.keyboard.press('Escape');
    const closed = await inPage(page, id => !document.getElementById(id).open, dialog);
    const returned = await inPage(page, k => document.activeElement && document.activeElement.dataset.efInvoker === k, key);
    await open(key, dialog);
    const trapped = await Array.from({ length: 8 }).reduce(async (acc) => { const ok = await acc; await page.keyboard.press('Tab'); return ok && (await contained(dialog)); }, Promise.resolve(true));
    await inPage(page, id => document.getElementById(id).close(), dialog);
    return [
      ...(opened ? [] : [`opening #${dialog} does not move focus into it`]),
      ...(closed ? [] : [`Escape does not close #${dialog}`]),
      ...(returned ? [] : [`closing #${dialog} does not return focus to its invoker`]),
      ...(trapped ? [] : [`Tab moves focus from the open dialog #${dialog} to page content outside it`]),
    ];
  };
  return invokers.reduce(async (acc, x) => [...(await acc), ...(await check(x))], Promise.resolve([]));
};

// ---- FL-05 reduced motion ------------------------------------------------------------------------------------------

// With prefers-reduced-motion, no running animation (CSS or script, document.getAnimations) and no transition lasts
// longer than 10 ms. The runner emulates reducedMotion: 'reduce' for this assertion.
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

// ---- FL-06 contrast, non-text contrast, forced colours, amber with a label --------------------------------------

const colourTools = () => {
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
  return { rgba, over, background, ratio };
};

// Text contrast at least 4.5:1, or 3:1 for large text, against the composited background.
export const textContrast = page =>
  inPage(page, tools => {
    const { rgba, over, background, ratio } = eval(`(${tools})()`);
    const hasText = e => [...e.childNodes].some(n => n.nodeType === 3 && n.textContent.trim() !== '');
    const visible = e => { const r = e.getBoundingClientRect(); const s = getComputedStyle(e); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && parseFloat(s.opacity) > 0; };
    return [...document.body.querySelectorAll('*')]
      .filter(e => hasText(e) && visible(e))
      .map(e => [e, getComputedStyle(e), background(e)])
      .filter(([, , bg]) => bg !== null)
      .map(([e, s, bg]) => {
        const size = parseFloat(s.fontSize);
        const large = size >= 24 || (size >= 18.66 && parseInt(s.fontWeight, 10) >= 700);
        return [e, ratio(over(rgba(s.color), bg), bg), large ? 3 : 4.5];
      })
      .filter(([, r, need]) => r < need - 0.005)
      .map(([e, r, need]) => `"${e.textContent.trim().slice(0, 30)}" has contrast ${r.toFixed(2)}:1 (needs ${need}:1)`);
  }, colourTools.toString());

// Non-text marks at least 3:1: form-control borders and the focus ring against their background.
export const nonTextContrast = page =>
  inPage(page, tools => {
    const { rgba, over, background, ratio } = eval(`(${tools})()`);
    const controls = [...document.querySelectorAll('input:not([type=hidden]), select, textarea')]
      .map(e => [e, getComputedStyle(e)])
      .filter(([, s]) => s.borderStyle !== 'none' && parseFloat(s.borderTopWidth) > 0)
      .map(([e, s]) => { const bg = background(e.parentElement || e); return bg ? [e, ratio(over(rgba(s.borderTopColor), bg), bg)] : null; })
      .filter(x => x && x[1] < 2.995)
      .map(([e, r]) => `${e.tagName.toLowerCase()} border has contrast ${r.toFixed(2)}:1 (needs 3:1)`);
    const focusables = [...document.querySelectorAll('button, a[href], input:not([type=hidden]), select, textarea')];
    // A focus indicator may be layered (Forma draws an outline in the page colour as a gap, outside a dark shadow
    // ring): the indicator is visible when its strongest component reaches 3:1.
    const ringColours = s => [
      ...(s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0 ? [s.outlineColor] : []),
      ...(s.boxShadow === 'none' ? [] : s.boxShadow.split(/,(?![^(]*\))/)).map(x => (x.match(/rgba?\([^)]*\)/) || [''])[0]).filter(Boolean),
    ].filter(c => rgba(c)[3] > 0);
    const rings = focusables
      .map(e => {
        e.focus({ focusVisible: true });
        const cs = ringColours(getComputedStyle(e));
        const bg = background(e.parentElement || e);
        e.blur();
        return [e, cs.length > 0 && bg ? Math.max(...cs.map(c => ratio(over(rgba(c), bg), bg))) : null];
      })
      .filter(([, r]) => r !== null && r < 2.995)
      .map(([e, r]) => `focus ring of ${e.tagName.toLowerCase()} has contrast ${r.toFixed(2)}:1 (needs 3:1)`);
    return [...controls, ...rings];
  }, colourTools.toString());

// Under forced colours every status indicator still carries text, and indicators of different states read differently.
// The runner emulates forcedColors: 'active' for this assertion.
export const forcedColorsDistinct = page =>
  inPage(page, () => {
    const lozenges = [...document.querySelectorAll('.ef-status-lozenge')];
    const text = e => (e.textContent || '').trim();
    const byState = new Map();
    lozenges.forEach(e => byState.set(e.dataset.state || '', [...(byState.get(e.dataset.state || '') || []), text(e)]));
    const states = [...byState.entries()];
    return [
      ...(lozenges.length === 0 ? ['no status indicator on the page'] : []),
      ...lozenges.filter(e => text(e) === '').map(() => 'a status indicator has no text under forced colours'),
      ...states.flatMap(([s, ts], k) => states.slice(k + 1).filter(([, us]) => us.some(u => ts.includes(u))).map(([u]) => `states "${s}" and "${u}" read the same`)),
    ];
  });

// Amber (#E9A23B and close shades) never stands alone: an amber element carries a text label (DEC-R7-002).
export const amberLabelled = page =>
  inPage(page, () => {
    const amber = [233, 162, 59];
    const near = c => { const v = (c.match(/[\d.]+/g) || []).map(Number); return v.length >= 3 && (v[3] === undefined || v[3] > 0) && Math.hypot(v[0] - amber[0], v[1] - amber[1], v[2] - amber[2]) < 40; };
    return [...document.body.querySelectorAll('*')]
      .filter(e => { const s = getComputedStyle(e); return near(s.backgroundColor) || near(s.borderTopColor) && parseFloat(s.borderTopWidth) > 0; })
      .filter(e => (e.textContent || '').trim() === '' && !e.getAttribute('aria-label'))
      .map(e => `${e.className || e.tagName} is amber with no text label`);
  });

// ---- FL-07 save-state indicator --------------------------------------------------------------------------------

// A status indicator is present, carries text (never colour alone) and is visible without scrolling.
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

// The indicator is composed as FL-07 states: a status lozenge, an operation status or conflict review, and a
// freshness line, together in the header region, each visible without scrolling.
export const indicatorComposed = page =>
  inPage(page, () => {
    const header = document.querySelector('.ef-record-header, header');
    const inView = e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && r.top < window.innerHeight && r.bottom > 0; };
    const region = header ? header.parentElement || document.body : null;
    const find = sel => (region ? region.querySelector(sel) : null);
    const parts = [['status lozenge', '.ef-status-lozenge'], ['operation status or conflict review', '.ef-operation-status, .ef-conflict-review'], ['freshness', '.ef-freshness']];
    return header === null
      ? ['no header region on the page']
      : parts.flatMap(([name, sel]) => { const e = find(sel); return e === null ? [`the header region has no ${name}`] : inView(e) ? [] : [`the ${name} is not visible without scrolling`]; });
  });

// ---- FL-08 loading and failures --------------------------------------------------------------------------------

// A loading placeholder tells assistive technology what is loading.
export const loadingAnnounced = page =>
  inPage(page, () => {
    const loaders = [...document.querySelectorAll('.ef-skeleton, [aria-busy=true], .ef-spinner')];
    const named = e => (e.textContent || '').trim() !== '' || e.getAttribute('aria-label') || e.getAttribute('aria-labelledby') || e.closest('[aria-label],[aria-labelledby]');
    return loaders.length === 0 ? ['no loading placeholder on the page'] : loaders.filter(e => !named(e)).map(e => `${e.className || e.tagName} has no accessible name or text`);
  });

// A fault states what happened in text and offers a next step (a control).
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

// A fault also says what was preserved (FL-08: "what happened, what was preserved and a safe next step").
export const faultStatesPreserved = page =>
  inPage(page, () => {
    const faults = [...document.querySelectorAll('[class*="ef-fault"], [role=alert], .ef-conflict-review')].filter(e => !e.parentElement.closest('[class*="ef-fault"], [role=alert], .ef-conflict-review'));
    const preserved = /\b(kept|preserved|queued|not lost|unchanged|no changes were (applied|made)|still (in|on)|remain(s)? (in|on))\b/i;
    return faults.length === 0 ? ['no fault on the page'] : faults.filter(e => !preserved.test(e.textContent || '')).map(e => `${e.className || 'alert'} does not say what was preserved`);
  });

// ---- FL-09 navigation ----------------------------------------------------------------------------------------------

// Navigation is reachable by keyboard: a visible labelled nav, or a visible disclosure that opens one; at most one
// current page per nav.
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
  const reach = async n => {
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

// At 48rem and narrower the navigation collapses through Forma's own pattern (the compact disclosure shows and the persistent
// list hides); at 48rem and wider the persistent list shows.
export const navigationCollapses = page =>
  inPage(page, () => {
    const shell = document.querySelector('.ef-navigation-shell');
    if (!shell) return ['no Forma navigation shell on the page'];
    const shows = sel => { const e = shell.querySelector(sel); if (!e) return false; const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(e).display !== 'none'; };
    // The same media query Forma's stylesheet uses, so the browser decides exactly as it applies the pattern.
    const narrow = window.matchMedia('(max-width: 48rem)').matches;
    const compact = shows('.ef-navigation-shell__compact');
    const persistent = shows('.ef-navigation-shell__persistent');
    return narrow
      ? [...(compact ? [] : ['at 48rem or narrower the compact navigation is not shown']), ...(persistent ? ['at 48rem or narrower the persistent navigation still shows'] : [])]
      : [...(persistent ? [] : ['wider than 48rem the persistent navigation is not shown']), ...(compact ? ['wider than 48rem the compact navigation still shows'] : [])];
  });

// ---- FL-10 tokens -------------------------------------------------------------------------------------------------

// No stylesheet other than Forma's, and no style attribute, declares an --ef-* property or uses one Forma does not
// define. Reads document.styleSheets (linked and inline) and every style attribute.
export const onlyFormaTokens = (page, { inventory }) =>
  inPage(page, known => {
    const sheetText = sheet => { try { return [...sheet.cssRules].map(r => r.cssText).join('\n'); } catch (e) { return ''; } };
    const sheets = [...document.styleSheets].filter(s => !(s.href && /\/dist\/all\.css$|forma[^/]*\.css$/.test(s.href))).map(sheetText);
    const attributes = [...document.querySelectorAll('[style]')].map(e => e.getAttribute('style'));
    const text = [...sheets, ...attributes].join('\n');
    const declared = [...text.matchAll(/(--ef-[a-z0-9-]+)\s*:/g)].map(m => m[1]);
    const used = [...text.matchAll(/var\(\s*(--ef-[a-z0-9-]+)/g)].map(m => m[1]);
    return [
      ...declared.map(p => `declares ${p}; only Forma defines --ef-* properties`),
      ...used.filter(p => !known.includes(p)).map(p => `uses ${p}, which Forma does not define`),
    ];
  }, inventory);

export const assertions = {
  touchTargets, noHorizontalScroll, scrollRegionsAccessible, textNotClipped, lineLength, focusVisible, focusOrder,
  skipLinkFirst, dialogFocus, reducedMotion, textContrast, nonTextContrast, forcedColorsDistinct, amberLabelled,
  statusVisible, indicatorComposed, loadingAnnounced, faultActionable, faultStatesPreserved, navigationReachable,
  navigationCollapses, onlyFormaTokens,
};
