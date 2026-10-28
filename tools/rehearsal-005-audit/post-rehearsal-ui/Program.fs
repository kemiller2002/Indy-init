/// Post-rehearsal browser probes (Rehearsal 5 implementation audit). Measures, in the frozen candidate's test build:
///  - UI-016-O11: WCAG contrast of every rendered text run against its effective background, and of thick
///    indicator borders (>= 3px) against the surface outside them, on each investigation view.
///  - UI-011-O4: whether scroll position and focus survive an in-page command re-render and a back navigation.
/// Output is JSON on stdout. Exit code 0 means the probe ran, not that the obligations are met.
module PostRehearsal.UiProbe

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Playwright

/// Runs in the page: every visible element with its own text, and every thick indicator border, with its contrast.
let private contrastScan =
    """() => {
  const parse = (c) => { const m = c.match(/rgba?\(([^)]+)\)/); if (!m) return null; const p = m[1].split(/[ ,\/]+/).filter(Boolean).map(Number); return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 }; };
  const lum = ({ r, g, b }) => { const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b); };
  const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };
  const hex = ({ r, g, b }) => "#" + [r, g, b].map((v) => Math.round(v).toString(16).padStart(2, "0")).join("").toUpperCase();
  const bgOf = (el) => { for (let e = el; e; e = e.parentElement) { const c = parse(getComputedStyle(e).backgroundColor); if (c && c.a >= 0.99) return c; } return { r: 255, g: 255, b: 255, a: 1 }; };
  const visible = (el) => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== "hidden" && s.display !== "none" && Number(s.opacity) > 0; };
  const name = (el) => el.tagName.toLowerCase() + (el.className && typeof el.className === "string" ? "." + el.className.trim().split(/\s+/).join(".") : "");
  const text = [];
  const borders = [];
  for (const el of document.querySelectorAll("body *")) {
    if (!visible(el)) continue;
    const s = getComputedStyle(el);
    const own = [...el.childNodes].some((n) => n.nodeType === 3 && n.textContent.trim().length > 0);
    if (own) {
      const fg = parse(s.color); const bg = bgOf(el);
      const size = parseFloat(s.fontSize); const bold = Number(s.fontWeight) >= 700;
      const large = size >= 24 || (bold && size >= 18.66);
      text.push({ el: name(el), fg: hex(fg), bg: hex(bg), ratio: Math.round(ratio(fg, bg) * 100) / 100, large, required: large ? 3 : 4.5 });
    }
    for (const side of ["Left", "Top", "Bottom"]) {
      const w = parseFloat(s["border" + side + "Width"]);
      if (w >= 3 && s["border" + side + "Style"] !== "none") {
        const bc = parse(s["border" + side + "Color"]); const outside = bgOf(el.parentElement ?? el);
        if (bc && bc.a > 0) borders.push({ el: name(el), side, border: hex(bc), against: hex(outside), ratio: Math.round(ratio(bc, outside) * 100) / 100, required: 3 });
      }
    }
  }
  const key = (o) => JSON.stringify([o.el, o.fg ?? o.border, o.bg ?? o.against, o.side ?? ""]);
  const uniq = (xs) => [...new Map(xs.map((x) => [key(x), x])).values()];
  return { text: uniq(text), borders: uniq(borders) };
}"""

let private await' (t: Task<'T>) = t.GetAwaiter().GetResult()
let private run (t: Task) = t.GetAwaiter().GetResult()

let private started (page: IPage) =
    run (page.Locator("#causa-boot").WaitForAsync(LocatorWaitForOptions(State = WaitForSelectorState.Detached, Timeout = Nullable 120000.0f)))

let private connectFixture (page: IPage) =
    let main = page.GetByRole(AriaRole.Main)
    [ "Fine-grained GitHub token", "not-used-in-fixture-mode"
      "Repository (owner/name)", "local/fixture"
      "Branch", "main"
      "Workspace", "synthetic-fixture" ]
    |> List.iter (fun (label, value) -> run (main.GetByLabel(label).FillAsync value))
    run (main.GetByRole(AriaRole.Button, LocatorGetByRoleOptions(Name = "Connect")).ClickAsync())
    run (page.Locator(".causa-fixture-mode").WaitForAsync(LocatorWaitForOptions(Timeout = Nullable 90000.0f)))

let private investigationUrl (page: IPage) (site: string) =
    run (page.GotoAsync(site + "index.html#/investigations"))
    let card = page.Locator(".causa-card a").First
    run (card.WaitForAsync(LocatorWaitForOptions(Timeout = Nullable 60000.0f)))
    run (card.ClickAsync())
    run (page.GetByRole(AriaRole.Heading, PageGetByRoleOptions(Level = Nullable 1)).WaitForAsync())
    Text.RegularExpressions.Regex.Match(page.Url, @"^.*#/investigations/[0-9a-f-]{36}").Value

let private settle (page: IPage) = run (page.WaitForTimeoutAsync 1500.0f)

let private scan (page: IPage) (url: string) =
    run (page.GotoAsync url)
    settle page
    let result = await' (page.EvaluateAsync<JsonElement>(contrastScan))
    url, result

let private scrollY (page: IPage) = await' (page.EvaluateAsync<float>("() => window.scrollY"))
let private activeName (page: IPage) =
    await' (page.EvaluateAsync<string>("() => { const a = document.activeElement; return a ? a.tagName.toLowerCase() + (a.id ? '#' + a.id : '') + (a.getAttribute('data-percepta-capability') ? '[' + a.getAttribute('data-percepta-capability') + ']' : '') : 'none'; }"))

/// Scroll to the observation form, run the command, and compare scroll position and focus after the re-render.
let private commandRerender (page: IPage) (overview: string) =
    run (page.GotoAsync overview)
    settle page
    let input = page.Locator("input[aria-labelledby=\"input-label-RecordObservation\"]")
    run (input.ScrollIntoViewIfNeededAsync())
    run (page.EvaluateAsync("() => window.scrollBy(0, 120)"))
    run (input.FillAsync "An observation recorded by the audit probe.")
    let button = page.Locator("button[data-percepta-capability=\"RecordObservation\"]").First
    run (button.FocusAsync())
    let top () = await' (button.EvaluateAsync<float>("b => b.getBoundingClientRect().top"))
    let beforeY, beforeFocus, beforeTop = scrollY page, activeName page, top ()
    run (page.Keyboard.PressAsync "Enter")
    run (page.Locator(".causa-sync [role=status]").WaitForAsync())
    settle page
    {| beforeScrollY = beforeY
       afterScrollY = scrollY page
       beforeFocus = beforeFocus
       afterFocus = activeName page
       controlViewportTopBefore = beforeTop
       controlViewportTopAfter = top () |}

/// Scroll down the overview, follow an in-app link to another view, go back, and compare.
let private backNavigation (page: IPage) (overview: string) =
    run (page.GotoAsync overview)
    settle page
    let height = await' (page.EvaluateAsync<float>("() => document.documentElement.scrollHeight - window.innerHeight"))
    run (page.EvaluateAsync(sprintf "() => window.scrollTo(0, %f)" (Math.Floor(height / 2.0))))
    settle page
    let beforeY = scrollY page
    run (page.GotoAsync(overview + "/timeline"))
    settle page
    run (page.GoBackAsync())
    settle page
    {| scrollable = height; beforeScrollY = beforeY; afterBackScrollY = scrollY page; focusAfterBack = activeName page |}

[<EntryPoint>]
let main argv =
    match argv with
    | [| site; chromium |] ->
        use pw = await' (Playwright.CreateAsync())
        let browser = await' (pw.Chromium.LaunchAsync(BrowserTypeLaunchOptions(ExecutablePath = chromium, Headless = Nullable true)))
        let page = await' (browser.NewPageAsync(BrowserNewPageOptions(ViewportSize = ViewportSize(Width = 1280, Height = 720))))
        run (page.GotoAsync(site + "index.html"))
        started page
        connectFixture page
        let overview = investigationUrl page site
        let views =
            [ overview
              overview + "/causal-analysis"
              overview + "/timeline"
              overview + "/evidence"
              overview + "/report"
              site + "index.html#/investigations"
              site + "index.html#/administration"
              site + "index.html#/settings" ]
        let contrast = views |> List.map (scan page) |> List.map (fun (u, r) -> {| view = u.Replace(site, ""); result = r |})
        let rerender = commandRerender page overview
        let back = backNavigation page overview
        let output =
            {| probe = "r5-ui-probe"; site = site; measuredAtUtc = DateTime.UtcNow.ToString("o"); contrast = contrast; scroll = {| commandRerender = rerender; backNavigation = back |} |}
        printfn "%s" (JsonSerializer.Serialize(output, JsonSerializerOptions(WriteIndented = true)))
        run (browser.CloseAsync())
        0
    | _ ->
        eprintfn "usage: r5-ui-probe <site-url-with-trailing-slash> <chromium-executable>"
        2
