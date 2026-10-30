// Broken pages: one or more deliberately defective page bodies per assertion. Each must make its assertion report at
// least one violation, which shows the assertion can fail (it is not vacuous). Several entries cover the common ways
// an invariant is broken (independent review rounds 3 and 4). Every fixture loads Forma's stylesheet like a
// real page.
//
// Conforming pages: bodies for invariants Forma's own pattern pages cannot show (composition of several patterns is
// built by the runner from Forma's pattern files; these are the few that need Causa-like content). Each must pass.

export const brokenFixtures = {
  touchTargets: [
    '<button style="all:unset;display:inline-block;width:20px;height:20px">x</button>',
    '<div onclick="void 0" style="width:10px;height:10px;background:#000"></div>',
  ],
  noHorizontalScroll: [
    '<div style="width:2400px">A row wider than any viewport.</div>',
    '<style>html,body{overflow-x:hidden}</style><div style="width:2400px">A row clipped by the page instead of scrolling.</div>',
  ],
  scrollRegionsAccessible: [
    '<div style="overflow-x:auto;width:200px"><div style="width:900px">Wide table content</div></div>',
    '<style>html,body{overflow-x:hidden}</style><table style="width:3000px"><tr><td>A wide table the page clips</td></tr></table>',
    '<div style="overflow:hidden;width:200px"><table style="width:3000px"><tr><td>wide</td></tr></table></div>',
    '<div tabindex="0" aria-label="Table" style="overflow-x:auto;width:200px;scrollbar-width:none"><div style="width:900px">Wide table</div></div>',
  ],
  textNotClipped: [
    '<p style="white-space:nowrap;overflow:hidden;width:60px">A long label that cannot be read in full</p>',
    '<p style="height:1em;overflow:hidden;width:120px">A paragraph clipped vertically after its first line of text here</p>',
    '<div style="overflow:hidden;width:80px"><span style="white-space:nowrap">Text in a span clipped by its parent box</span></div>',
    '<p style="width:120px;display:-webkit-box;-webkit-line-clamp:1;-webkit-box-orient:vertical;overflow:hidden">Saved changes are kept in this tab until you reconnect</p>',
  ],
  lineLength: [
    '<article class="ef-prose" style="max-width:none"><p style="max-width:none;width:120ch">' + 'Unbounded line length makes long text hard to read. '.repeat(6) + '</p></article>',
    '<p style="max-inline-size:none;max-width:none;width:120ch">' + 'Running text outside any reading region is still running text. '.repeat(6) + '</p>',
  ],
  focusVisible: [
    '<style>*:focus,*:focus-visible{outline:2px solid transparent!important;box-shadow:0 1px 2px rgba(0,0,0,.4)!important}button,a{box-shadow:0 1px 2px rgba(0,0,0,.4)}</style><button>Save</button><a href="#x">Details</a>',
    '<style>button:focus-visible{outline:3px solid #fff!important;outline-offset:0;box-shadow:none!important}</style><div style="background:#fff"><button>Save</button></div>',
  ],
  focusOrder: '<button>First</button><button tabindex="3">Jumps ahead</button><button>Third</button>',
  skipLinkFirst: [
    '<main id="main"><button>Act</button></main><a href="#main">Skip to main content</a>',
    '<a href="#m" style="position:absolute;left:-9999px">Skip to content</a><main id="m"><button>Act</button></main>',
  ],
  dialogFocus: [
    '<dialog id="d2"><button>Close</button></dialog><button type="button" aria-haspopup="dialog" aria-controls="d2" onclick="document.getElementById(\'d2\').show()">Open non-modal</button><button>Outside</button>',
    '<dialog id="d3"><button>Close</button></dialog><button type="button" aria-haspopup="dialog" aria-controls="d3">Does nothing</button>',
    '<button type="button" commandfor="d1" command="show-modal">Open</button><div id="d1" role="dialog"><p>Not a dialog element</p><button>OK</button></div>',
  ],
  reducedMotion: [
    '<div class="spinning">Loading</div><script>document.querySelector(".spinning").animate([{transform:"rotate(0)"},{transform:"rotate(1turn)"}],{duration:2000,iterations:Infinity})</script>',
    '<div id="late">Loading</div><script>setTimeout(()=>document.getElementById("late").animate([{opacity:0},{opacity:1}],{duration:2000,iterations:Infinity}),300)</script>',
  ],
  textContrast: [
    '<p style="color:#c8c8c8;background:#ffffff">Pale text on white</p>',
    '<div style="background:#ffffff"><div style="opacity:0.25"><p style="color:#000000">Text faded by an ancestor</p></div></div>',
  ],
  nonTextContrast: '<div style="background:#ffffff;color:#1b1b1b;padding:8px"><label>Name <input style="border:1px solid #eeeeee;background:#ffffff"></label></div>',
  forcedColorsDistinct: [
    '<span class="ef-status-lozenge" data-state="saved">Saved</span><span class="ef-status-lozenge" data-state="unknown">Saved</span><span class="ef-status-lozenge" data-state="offline"></span>',
    '<span class="ef-status-lozenge" style="background:green">Status</span><span class="ef-status-lozenge" style="background:red">Status</span>',
  ],
  amberLabelled: '<span style="display:inline-block;width:12px;height:12px;border-radius:50%;background:#E9A23B"></span>',
  statusVisible: [
    '<div style="height:4000px"></div><span class="ef-status-lozenge"></span>',
    '<span class="ef-status-lozenge" style="color:transparent">Saved</span>',
  ],
  indicatorComposed: [
    '<header class="ef-record-header"><h2>Investigation</h2><span class="ef-status-lozenge" data-state="saved">Saved</span></header>',
    '<header class="ef-record-header"><h2>Investigation</h2></header><main><p>Body</p></main><footer><span class="ef-status-lozenge">Saved</span><p class="ef-freshness">Saved at 10:04</p><section class="ef-operation-status"><p>Saving</p></section></footer>',
    '<header class="ef-record-header"><h2>Investigation</h2><span class="ef-status-lozenge">Unsaved</span></header><section class="ef-operation-status"><p>Saving</p></section><p class="ef-freshness">Saved at 10:04</p>',
  ],
  loadingAnnounced: '<div class="ef-skeleton" aria-busy="true"></div>',
  faultActionable: '<div role="alert">Error</div>',
  faultStatesPreserved: [
    '<div role="alert"><p>Save failed: GitHub refused the change.</p><button type="button">Retry</button></div>',
    '<div role="alert"><p>Save failed. Nothing was kept.</p><button type="button">Retry</button></div>',
    '<div role="alert"><p>Your changes were lost.</p><button type="button">Start again</button></div>',
  ],
  navigationReachable: [
    '<div><a href="#a">Somewhere</a></div>',
    '<details><summary>Menu</summary><nav aria-label="Primary"></nav></details>',
  ],
  navigationCollapses: '<style>.ef-navigation-shell__compact{display:none!important}.ef-navigation-shell__persistent{display:none!important}</style><div class="ef-navigation-shell"><details class="ef-navigation-shell__compact"><summary>Navigation</summary><nav aria-label="Primary"><a href="#a">A</a></nav></details><nav class="ef-navigation-shell__persistent" aria-label="Primary"><a href="#a">A</a></nav></div>',
  onlyFormaTokens: [
    '<style>:root{--ef-causa-gap:4px}p{margin:var(--ef-not-in-forma)}</style><p style="padding:var(--ef-also-invented)">Invented tokens</p>',
    '<p style="margin:var(--ef-not-in-forma)">Invented token in a style attribute</p>',
    '<link rel="stylesheet" href="data:text/css,:root%7B--ef-causa-gap:4px%7D"><p>Invented token in a linked stylesheet</p>',
  ],
};

export const conformingFixtures = {
  amberLabelled: '<span class="ef-status-lozenge" style="background:#E9A23B;color:#1b1b1b">Pending review</span>',
  faultStatesPreserved: '<aside class="ef-alert ef-fault-banner" role="status"><h2 class="ef-alert__title">Save failed</h2><p>GitHub refused the change. Your changes are kept in this tab.</p><div class="ef-recovery-actions" role="group" aria-label="Recovery actions"><button type="button">Retry</button></div></aside>',
};
