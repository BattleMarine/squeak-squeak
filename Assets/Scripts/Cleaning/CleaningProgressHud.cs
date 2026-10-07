using UnityEngine;
using UnityEngine.UI;

namespace SqueakSqueak.Cleaning
{
    /// <summary>
    /// 청소 진행 상태를 화면 텍스트로 표시합니다.
    /// HUD는 진행 상태를 변경하지 않고 표시값만 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CleaningProgressHud : MonoBehaviour
    {
        [SerializeField]
        private CleaningProgressTracker progressTracker;

        [SerializeField]
        private Text objectDirtText;

        [SerializeField]
        private Text surfaceDirtText;

        [SerializeField]
        private Text polishText;

        [SerializeField]
        private Text timerText;

        [SerializeField] private CleaningCompletionController completion;
        [SerializeField] private CleaningToolSelection toolSelection;
        [SerializeField] private CoinSurfacePointer surfacePointer;
        [SerializeField] private Text selectedToolText;
        [SerializeField] private Text faceHintText;
        [SerializeField] private Text nextActionText;

        private int lastObjectDirtPercent = -1;
        private int lastSurfaceDirtPercent = -1;
        private int lastPolishPercent = -1;
        private int lastElapsedSeconds = -1;
        private int lastObjectTarget = -1, lastSurfaceTarget = -1, lastPolishTarget = -1;
        private CleaningToolDefinition lastTool;
        private bool toolDisplayed;

        private void Awake()
        {
            if (progressTracker != null && objectDirtText != null && surfaceDirtText != null && polishText != null && timerText != null &&
                completion != null && toolSelection != null && surfacePointer != null &&
                selectedToolText != null && faceHintText != null && nextActionText != null)
            {
                return;
            }

            Debug.LogError("CleaningProgressHud requires tracker and all progress Text references.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            progressTracker.ProgressChanged += HandleProgressChanged;
            ResetDisplayedValues();
            Refresh();
        }

        private void OnDisable()
        {
            if (progressTracker != null)
            {
                progressTracker.ProgressChanged -= HandleProgressChanged;
            }
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void HandleProgressChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            var objectDirtPercent = ToPercent(progressTracker.ObjectDirtRemovalRatio);
            var objectTarget = ToPercent(completion.RequiredObjectDirtRemovalRatio);
            if (objectDirtPercent != lastObjectDirtPercent || objectTarget != lastObjectTarget)
            {
                lastObjectDirtPercent = objectDirtPercent;
                lastObjectTarget = objectTarget;
                objectDirtText.text = $"큰 먼지 제거: {objectDirtPercent}% / 목표 {objectTarget}%";
            }

            var surfaceDirtPercent = ToPercent(progressTracker.SurfaceDirtRemovalRatio);
            var surfaceTarget = ToPercent(completion.RequiredSurfaceDirtRemovalRatio);
            if (surfaceDirtPercent != lastSurfaceDirtPercent || surfaceTarget != lastSurfaceTarget)
            {
                lastSurfaceDirtPercent = surfaceDirtPercent;
                lastSurfaceTarget = surfaceTarget;
                surfaceDirtText.text = $"얼룩 제거: {surfaceDirtPercent}% / 목표 {surfaceTarget}%";
            }

            var polishPercent = ToPercent(progressTracker.Polish);
            var polishTarget = ToPercent(completion.RequiredPolish);
            if (polishPercent != lastPolishPercent || polishTarget != lastPolishTarget)
            {
                lastPolishPercent = polishPercent;
                lastPolishTarget = polishTarget;
                polishText.text = $"광택: {polishPercent}% / 목표 {polishTarget}%";
            }

            var elapsedSeconds = Mathf.FloorToInt(progressTracker.ElapsedSeconds);
            if (elapsedSeconds != lastElapsedSeconds)
            {
                lastElapsedSeconds = elapsedSeconds;
                timerText.text = $"청소 시간: {elapsedSeconds / 60:00}:{elapsedSeconds % 60:00}";
            }

            // 안내는 현재 소유자 상태를 읽기만 합니다. 완료 전환/청소·입력·타이머를 쓰지 않습니다.
            var tool = toolSelection.CurrentTool;
            if (!toolDisplayed || tool != lastTool)
            {
                var name = tool == null ? "도구 없음" : tool.DisplayName;
                var role = tool == null ? "" : tool.ToolId == CleaningToolId.SmallBrush ? "큰 먼지 제거" :
                    tool.ToolId == CleaningToolId.PolishingCloth ? "마무리 광택" : "얼룩 닦기";
                SetTextIfChanged(selectedToolText, $"선택: {name} · {role}");
                lastTool = tool; toolDisplayed = true;
            }
            var face = surfacePointer.HasSurfaceHit ? surfacePointer.CurrentSurfaceContact.Face : CoinSurfaceFace.Invalid;
            SetTextIfChanged(faceHintText, face == CoinSurfaceFace.Front ? "앞면(태양) · 뒷면도 돌려서 닦아주세요" :
                face == CoinSurfaceFace.Back ? "뒷면(물결) · 앞면도 함께 닦아주세요" :
                face == CoinSurfaceFace.Side ? "옆면은 닦지 않아요 · MMB로 돌려주세요" : "태양 앞면 · 물결 뒷면, 두 면 모두 닦기");
            SetTextIfChanged(nextActionText, completion.IsCompleted ? "완료! 결과에서 새 청소를 시작하세요" :
                progressTracker.ObjectDirtRemovalRatio < completion.RequiredObjectDirtRemovalRatio ? "다음: 작은 솔로 큰 먼지를 털어내세요" :
                progressTracker.SurfaceDirtRemovalRatio < completion.RequiredSurfaceDirtRemovalRatio ? "다음: 마른 천으로 앞뒤 얼룩을 닦으세요" :
                progressTracker.Polish < completion.RequiredPolish ? "다음: 광택천으로 앞뒤를 마무리하세요" : "마무리 확인 중");
        }

        private static void SetTextIfChanged(Text label, string value) { if (label.text != value) label.text = value; }

        private void ResetDisplayedValues()
        {
            lastObjectDirtPercent = -1;
            lastSurfaceDirtPercent = -1;
            lastPolishPercent = -1;
            lastElapsedSeconds = -1;
            lastObjectTarget = lastSurfaceTarget = lastPolishTarget = -1;
            toolDisplayed = false;
        }

        private static int ToPercent(float ratio)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f);
        }
    }
}
