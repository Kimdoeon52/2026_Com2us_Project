using System.Collections.Generic;

// 복권 3×3 판 생성. 가로줄 하나만 같은 부품 3칸이고 나머지 줄·세로·대각선은 겹치지 않는다
public static class ScratchLotteryBoard
{
    public const int Size = 3;
    public const int CellCount = Size * Size;

    private const int MaxAttempts = 64;

    private static readonly int[][] Lines =
    {
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 },
    };

    private static readonly List<ComponentDefinition> Candidates = new List<ComponentDefinition>();

    // 당첨 부품은 winGrade 중에서, 나머지 칸은 기본 등급을 뺀 전체에서 뽑는다
    public static bool TryBuild(ComponentCatalog catalog, PartGrade winGrade, Rng rng, ComponentDefinition[] cells, out int winRow)
    {
        winRow = -1;

        if (catalog == null || catalog.Components == null || rng == null || cells == null || cells.Length < CellCount)
            return false;

        if (!catalog.TryPick(winGrade, rng, out ComponentDefinition winner))
            return false;

        Candidates.Clear();
        foreach (ComponentDefinition c in catalog.Components)
        {
            if (c != null && c.Grade != PartGrade.Basic)
                Candidates.Add(c);
        }

        if (Candidates.Count < 2)
            return false;

        winRow = rng.Range(0, Size);
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                    cells[(row * Size) + col] = row == winRow ? winner : Candidates[rng.Range(0, Candidates.Count)];
            }

            if (IsValid(cells, winRow))
                return true;
        }

        winRow = -1;
        return false;
    }

    private static bool IsValid(ComponentDefinition[] cells, int winRow)
    {
        for (int row = 0; row < Size; row++)
        {
            if (row != winRow && AllSame(cells, row * Size, (row * Size) + 1, (row * Size) + 2))
                return false;
        }

        foreach (int[] line in Lines)
        {
            if (AllSame(cells, line[0], line[1], line[2]))
                return false;
        }

        return true;
    }

    private static bool AllSame(ComponentDefinition[] cells, int a, int b, int c)
    {
        return cells[a] == cells[b] && cells[b] == cells[c];
    }
}
