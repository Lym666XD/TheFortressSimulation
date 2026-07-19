namespace HumanFortress.App.UI;

/// <summary>
/// Renders the Zones quick menu with L2 and L3 submenus, zone overlay, and detail popup.
/// </summary>
internal sealed partial class ZonesUI
{
    private int? _detailPopupZoneId = null;

    public int? DetailPopupZoneId => _detailPopupZoneId;

    /// <summary>
    /// Open zone detail popup.
    /// </summary>
    public void OpenDetailPopup(int zoneId)
    {
        _detailPopupZoneId = zoneId;
    }

    /// <summary>
    /// Close zone detail popup.
    /// </summary>
    public void CloseDetailPopup()
    {
        _detailPopupZoneId = null;
    }

    /// <summary>
    /// Check if detail popup is open.
    /// </summary>
    public bool IsDetailPopupOpen()
    {
        return _detailPopupZoneId.HasValue;
    }
}
