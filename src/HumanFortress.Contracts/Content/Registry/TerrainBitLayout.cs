using System;
using System.Collections.Generic;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Describes the bit layout of the TerrainBits field.
/// </summary>
public class TerrainBitLayout
{
    public int TotalBits { get; set; } = 16;
    public List<BitFieldDefinition> Fields { get; set; } = new();

    public void Validate()
    {
        if (TotalBits != 16)
            throw new InvalidOperationException("TerrainBits must be 16 bits");

        for (int i = 0; i < Fields.Count; i++)
        {
            var field1 = Fields[i];
            for (int j = i + 1; j < Fields.Count; j++)
            {
                var field2 = Fields[j];
                if (BitRangesOverlap(field1, field2))
                {
                    throw new InvalidOperationException(
                        $"Bit fields '{field1.Name}' and '{field2.Name}' overlap");
                }
            }
        }

        foreach (var field in Fields)
        {
            if (field.StartBit + field.BitCount > TotalBits)
            {
                throw new InvalidOperationException(
                    $"Bit field '{field.Name}' exceeds total bit count");
            }
        }
    }

    private static bool BitRangesOverlap(BitFieldDefinition a, BitFieldDefinition b)
    {
        int aEnd = a.StartBit + a.BitCount - 1;
        int bEnd = b.StartBit + b.BitCount - 1;
        return !(aEnd < b.StartBit || bEnd < a.StartBit);
    }
}

public class BitFieldDefinition
{
    public string Name { get; set; } = "";
    public int StartBit { get; set; }
    public int BitCount { get; set; }
    public string Description { get; set; } = "";
    public Dictionary<string, int> Values { get; set; } = new();
}
