// One deliberately broken page body per assertion. Each must make its assertion report at least one violation, which
// shows the assertion can fail (it is not vacuous). Every fixture loads Forma's stylesheet like a real page.

export const brokenFixtures = {
  touchTargets: '<button style="all:unset;display:inline-block;width:20px;height:20px">x</button>',
  noHorizontalScroll: '<div style="width:2400px">A row wider than any viewport.</div>',
  textNotClipped: '<p style="white-space:nowrap;overflow:hidden;width:60px">A long label that cannot be read in full</p>',
  focusVisible: '<style>*:focus,*:focus-visible{outline:none!important;box-shadow:none!important}</style><button>Save</button><a href="#x">Details</a>',
  // Forma zeroes CSS animation under prefers-reduced-motion from inside its cascade layer, which a page stylesheet
  // cannot override; the realistic failure is a script animation that ignores the preference.
  reducedMotion: '<div class="spinning">Loading</div><script>document.querySelector(".spinning").animate([{transform:"rotate(0)"},{transform:"rotate(1turn)"}],{duration:2000,iterations:Infinity})</script>',
  textContrast: '<p style="color:#c8c8c8;background:#ffffff">Pale text on white</p>',
  statusVisible: '<div style="height:4000px"></div><span class="ef-status-lozenge"></span>',
  loadingAnnounced: '<div class="ef-skeleton" aria-busy="true"></div>',
  faultActionable: '<div role="alert">Error</div>',
  navigationReachable: '<div><a href="#a">Somewhere</a></div>',
  onlyFormaTokens: '<style>:root{--ef-causa-gap:4px}p{margin:var(--ef-not-in-forma)}</style><p>Invented tokens</p>',
};
