using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 3×3 복권. 다 긁으면 가로 당첨 줄의 부품을 보상으로 돌려준다
public class ScratchCardMinigame : ScrapMinigameBase
{
    [Header("긁기")]
    [Tooltip("긁기 표면")]
    [SerializeField] private ScratchSurface _surface;

    [Tooltip("성공으로 인정할 긁힘 비율. 1로 두면 한 픽셀만 남아도 끝나지 않는다")]
    [Range(0f, 1f)]
    [SerializeField] private float _requiredCleared01 = 0.95f;

    [Header("복권")]
    [Tooltip("칸에 적을 부품 목록")]
    [SerializeField] private ComponentCatalog _catalog;

    [Tooltip("당첨 줄 등급")]
    [SerializeField] private PartGrade _winGrade = PartGrade.Rare;

    [Tooltip("다 긁은 뒤 결과를 보여주는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float _revealDuration = 1.2f;

    [Header("연출")]
    [Tooltip("칸을 깔 영역. 긁기 표면 아래에 둔다")]
    [SerializeField] private Image _rewardImage;

    [Tooltip("판 바탕색")]
    [SerializeField] private Color _boardColor = new Color(0.12f, 0.12f, 0.13f);

    [Tooltip("칸 바탕색")]
    [SerializeField] private Color _cellColor = new Color(0.2f, 0.2f, 0.22f);

    [Tooltip("당첨 줄 칸 바탕색")]
    [SerializeField] private Color _winColor = new Color(0.55f, 0.45f, 0.12f);

    [Tooltip("칸 글자색")]
    [SerializeField] private Color _labelColor = Color.white;

    [Tooltip("칸 글꼴. 비우면 기본 글꼴")]
    [SerializeField] private TMP_FontAsset _labelFont;

    [Tooltip("칸 사이 여백")]
    [Min(0f)]
    [SerializeField] private float _cellPadding = 4f;

    [Tooltip("난수 시드. 0이면 실행할 때마다 달라진다")]
    [SerializeField] private int _seed;

    private readonly ComponentDefinition[] _cells = new ComponentDefinition[ScratchLotteryBoard.CellCount];
    private readonly Image[] _cellImages = new Image[ScratchLotteryBoard.CellCount];
    private readonly TMP_Text[] _cellLabels = new TMP_Text[ScratchLotteryBoard.CellCount];

    private Rng _rng;
    private int _winRow = -1;
    private float _revealTimer = -1f;

    protected override void OnBegin()
    {
        if (_rng == null)
            _rng = new Rng(_seed != 0 ? _seed : System.Environment.TickCount);

        _revealTimer = -1f;
        BuildBoard();

        if (_surface == null)
            return;

        _surface.Rebuild();
        _surface.SetInputEnabled(true);
    }

    // 판을 못 만들면 칸을 비우고 일반 긁기로 진행한다
    private void BuildBoard()
    {
        if (!ScratchLotteryBoard.TryBuild(_catalog, _winGrade, _rng, _cells, out _winRow))
        {
            Debug.LogWarning($"[{name}] 복권 판을 만들지 못했다. 부품 목록과 당첨 등급을 확인할 것.", this);
            System.Array.Clear(_cells, 0, _cells.Length);
        }

        if (_rewardImage != null)
            _rewardImage.color = _boardColor;

        for (int i = 0; i < ScratchLotteryBoard.CellCount; i++)
        {
            EnsureCell(i);
            _cellImages[i].color = _cellColor;
            _cellLabels[i].text = _cells[i] != null ? CellLabel(_cells[i].DisplayName) : string.Empty;
        }
    }

    // 칸이 좁아 글자 단위로 끊기지 않게 첫 띄어쓰기에서만 줄을 나눈다
    private static string CellLabel(string displayName)
    {
        int space = displayName.IndexOf(' ');
        return space < 0 ? displayName : displayName.Substring(0, space) + "\n" + displayName.Substring(space + 1);
    }

    // 칸은 처음 한 번만 만들고 재사용한다
    private void EnsureCell(int index)
    {
        if (_cellImages[index] != null || _rewardImage == null)
            return;

        int row = index / ScratchLotteryBoard.Size;
        int col = index % ScratchLotteryBoard.Size;
        float step = 1f / ScratchLotteryBoard.Size;

        var cell = new GameObject($"Cell_{index}", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)cell.transform;
        rect.SetParent(_rewardImage.transform, false);
        rect.anchorMin = new Vector2(col * step, 1f - ((row + 1) * step));
        rect.anchorMax = new Vector2((col + 1) * step, 1f - (row * step));
        rect.offsetMin = new Vector2(_cellPadding, _cellPadding);
        rect.offsetMax = new Vector2(-_cellPadding, -_cellPadding);

        var image = cell.GetComponent<Image>();
        image.raycastTarget = false;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(_cellPadding, _cellPadding);
        labelRect.offsetMax = new Vector2(-_cellPadding, -_cellPadding);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        if (_labelFont != null)
            label.font = _labelFont;

        label.color = _labelColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 28f;
        label.raycastTarget = false;

        _cellImages[index] = image;
        _cellLabels[index] = label;
    }

    protected override void OnTick()
    {
        if (_surface == null)
            return;

        if (_revealTimer >= 0f)
        {
            _revealTimer -= Time.deltaTime;
            if (_revealTimer < 0f)
                FinishWin();

            return;
        }

        if (_surface.Cleared01 >= _requiredCleared01)
            StartReveal();
    }

    // 남은 덮개를 걷고 당첨 줄을 강조한 채 잠깐 보여준다
    private void StartReveal()
    {
        _surface.SetInputEnabled(false);
        _surface.RevealAll();

        if (_winRow >= 0)
        {
            for (int col = 0; col < ScratchLotteryBoard.Size; col++)
                _cellImages[(_winRow * ScratchLotteryBoard.Size) + col].color = _winColor;
        }

        _revealTimer = _revealDuration;
        if (_revealTimer <= 0f)
            FinishWin();
    }

    private void FinishWin()
    {
        _revealTimer = -1f;
        string rewardId = _winRow >= 0 ? _cells[_winRow * ScratchLotteryBoard.Size].Id : null;
        Finish(ScrapMinigameResult.Win(1f, rewardId));
    }

    // 결과를 보여주는 중에 시간이 다 되면 당첨으로 끝낸다
    protected override void OnTimeout()
    {
        if (_revealTimer >= 0f)
        {
            FinishWin();
            return;
        }

        base.OnTimeout();
    }

    protected override void OnFinish()
    {
        if (_surface != null)
            _surface.SetInputEnabled(false);
    }

    protected override float PerformanceOnTimeout() => _surface != null ? _surface.Cleared01 : 0f;
}
