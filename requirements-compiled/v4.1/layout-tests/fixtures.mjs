// Broken pages: one deliberately defective page body per assertion. Each must make its assertion report at least one
// violation, which shows the assertion can fail (it is not vacuous). Every fixture loads Forma's stylesheet like a
// real page.
//
// Conforming pages: bodies for invariants Forma's own pattern pages cannot show (composition of several patterns is
// built by the runner from Forma's pattern files; these are the few that need Causa-like content). Each must pass.

export const brokenFixtures = {
  touchTargets: '<button style="all:unset;display:inline-block;width:20px;height:20px">x</button>',
  noHorizontalScroll: '<div style="width:2400px">A row wider than any viewport.</div>',
  scrollRegionsAccessible: '<div style="overflow-x:auto;width:200px"><div style="width:900px">Wide table content</div></div>',
  textNotClipped: '<p style="white-space:nowrap;overflow:hidden;width:60px">A long label that cannot be read in full</p><p style="height:1em;overflow:hidden;width:120px">A paragraph clipped vertically after its first line of text here</p>',
  lineLength: '<article class="ef-prose" style="max-width:none"><p style="max-width:none;width:120ch">' + 'Unbounded line length makes long text hard to read. '.repeat(6) + '</p></article>',
  focusVisible: '<style>*:focus,*:focus-visible{outline:2px solid transparent!important;box-shadow:0 1px 2px rgba(0,0,0,.4)!important}button,a{box-shadow:0 1px 2px rgba(0,0,0,.4)}</style><button>Save</button><a href="#x">Details</a>',
  focusOrder: '<button>First</button><button tabindex="3">Jumps ahead</button><button>Third</button>',
  skipLinkFirst: '<main id="main"><button>Act</button></main><a href="#main">Skip to main content</a>',
  dialogFocus: '<button type="button" commandfor="d1" command="show-modal">Open</button><div id="d1" role="dialog"><p>Not a dialog element</p><button>OK</button></div><dialog id="d2"><button>Close</button></dialog><button type="button" aria-haspopup="dialog" aria-controls="d2" onclick="document.getElementById(\'d2\').show()">Open non-modal</button>',
  reducedMotion: '<div class="spinning">Loading</div><script>document.querySelector(".spinning").animate([{transform:"rotate(0)"},{transform:"rotate(1turn)"}],{duration:2000,iterations:Infinity})</script>',
  textContrast: '<p style="color:#c8c8c8;background:#ffffff">Pale text on white</p>',
  nonTextContrast: '<div style="background:#ffffff;color:#1b1b1b;padding:8px"><label>Name <input style="border:1px solid #eeeeee;background:#ffffff"></label></div>',
  forcedColorsDistinct: '<span class="ef-status-lozenge" data-state="saved">Saved</span><span class="ef-status-lozenge" data-state="unknown">Saved</span><span class="ef-status-lozenge" data-state="offline"></span>',
  amberLabelled: '<span style="display:inline-block;width:12px;height:12px;border-radius:50%;background:#E9A23B"></span>',
  statusVisible: '<div style="height:4000px"></div><span class="ef-status-lozenge"></span>',
  indicatorComposed: '<header class="ef-record-header"><h2>Investigation</h2><span class="ef-status-lozenge" data-state="saved">Saved</span></header>',
  loadingAnnounced: '<div class="ef-skeleton" aria-busy="true"></div>',
  faultActionable: '<div role="alert">Error</div>',
  faultStatesPreserved: '<div role="alert"><p>Save failed: GitHub refused the change.</p><button type="button">Retry</button></div>',
  navigationReachable: '<div><a href="#a">Somewhere</a></div>',
  navigationCollapses: '<style>.ef-navigation-shell__compact{display:none!important}.ef-navigation-shell__persistent{display:none!important}</style><div class="ef-navigation-shell"><details class="ef-navigation-shell__compact"><summary>Navigation</summary><nav aria-label="Primary"><a href="#a">A</a></nav></details><nav class="ef-navigation-shell__persistent" aria-label="Primary"><a href="#a">A</a></nav></div>',
  onlyFormaTokens: '<style>:root{--ef-causa-gap:4px}p{margin:var(--ef-not-in-forma)}</style><p style="padding:var(--ef-also-invented)">Invented tokens</p>',
};

export const conformingFixtures = {
  amberLabelled: '<span class="ef-status-lozenge" style="background:#E9A23B;color:#1b1b1b">Pending review</span>',
  faultStatesPreserved: '<aside class="ef-alert ef-fault-banner" role="status"><h2 class="ef-alert__title">Save failed</h2><p>GitHub refused the change. Your changes are kept in this tab.</p><div class="ef-recovery-actions" role="group" aria-label="Recovery actions"><button type="button">Retry</button></div></aside>',
};
