using System.Collections.Generic;

namespace HumanFortress.App.UI;

/// <summary>
/// Renders the Zones quick menu with L2 and L3 submenus, zone overlay, and detail popup.
/// </summary>
internal sealed partial class ZonesUI
{
    private int? _detailPopupZoneId = null;

    private readonly Dictionary<ZoneSubmenu, Dictionary<char, string>> _zoneKeyMappings = new()
    {
        [ZoneSubmenu.Production] = new()
        {
            ['z'] = "lumbering",
            ['x'] = "gather_plants",
            ['c'] = "fishing",
            ['v'] = "sand_clay",
            ['r'] = "pasture"
        },
        [ZoneSubmenu.Civil] = new()
        {
            ['z'] = "bedroom",
            ['x'] = "dormitory",
            ['c'] = "dining_hall",
            ['v'] = "bathhouse",
            ['g'] = "tomb"
        },
        [ZoneSubmenu.Public] = new()
        {
            ['z'] = "assembly",
            ['c'] = "temple",
            ['v'] = "tavern",
            ['t'] = "hospital",
            ['f'] = "office",
            ['g'] = "library"
        },
        [ZoneSubmenu.Military] = new()
        {
            ['z'] = "military_grounds"
        },
        [ZoneSubmenu.Management] = new()
        {
            ['z'] = "burrow",
            ['x'] = "restricted_traffic"
        }
    };

    public int? DetailPopupZoneId => _detailPopupZoneId;

    /// <summary>
    /// Try to get zone definition ID from keyboard input.
    /// </summary>
    public string? GetZoneDefIdFromKey(ZoneSubmenu submenu, char key)
    {
        if (_zoneKeyMappings.TryGetValue(submenu, out var mapping))
        {
            if (mapping.TryGetValue(key, out var defId))
            {
                return defId;
            }
        }

        return null;
    }

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
