namespace Causa.Ui.Contracts

/// Stable identifiers. Catalog entries keep their CAT-CAUSA-INVESTIGATION id (INV-0xx) as their field, action or region id,
/// so a UI element, its manifest entry and its Dokimos/catalog check share one name. Entries no catalog item names (the
/// connection form, journey-named actions) get a UIF-/UIA-/UIR- id and cite the source that requires them
/// (SCREEN-MANIFEST.json). The id is also the Limen binding name (data-text, data-event) of the element.
type ScreenId = ScreenId of string
type FieldId = FieldId of string
type ActionId = ActionId of string
type RegionId = RegionId of string
