using System.Drawing;

// One palette for ground names, tooltips, pet filters and loot materials.
// Added stats are shown in the tooltip; they do not promote the database grade.
public static class ItemQualityPresentation
{
    public static ItemGrade Normalize(ItemGrade grade) => (byte)grade <= (byte)ItemGrade.Heroic ? grade : ItemGrade.None;
    public static Color Colour(ItemGrade grade) => Normalize(grade) switch
    {
        ItemGrade.Rare => Color.FromArgb(65,195,255),
        ItemGrade.Legendary => Color.FromArgb(255,176,54),
        ItemGrade.Mythical => Color.FromArgb(202,112,255),
        ItemGrade.Heroic => Color.FromArgb(255,85,93),
        _ => Color.FromArgb(240,237,224)
    };
}
