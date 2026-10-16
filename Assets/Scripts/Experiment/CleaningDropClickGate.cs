namespace SqueakSqueak.Cleaning
{
    /// <summary>차단 후 놓았다 다시 눌러야 합니다. 프레임별 조회는 소모하지 않습니다.</summary>
    public sealed class CleaningDropClickGate
    {
        private bool armed;
        private long frame = -1;
        private bool cached;
        public void RequireRelease() { armed = false; frame = -1; cached = false; }
        public bool Sample(long currentFrame, bool pressed, bool eligible)
        {
            if (frame == currentFrame) return cached;
            frame = currentFrame; cached = false;
            if (!pressed) { armed = true; return false; }
            if (!eligible) { armed = false; return false; }
            if (!armed) return false;
            armed = false; cached = true; return true;
        }
    }
}
