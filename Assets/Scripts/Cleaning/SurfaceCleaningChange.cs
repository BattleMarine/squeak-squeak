namespace SqueakSqueak.Cleaning
{
    /// <summary>실제 변경 한 번의 읽기 전용 표현/오디오 정보. 청소 상태를 소유하지 않습니다.</summary>
    public readonly struct SurfaceCleaningChange
    {
        public CoinSurfaceContact Contact { get; }
        public CleaningToolId Tool { get; }
        public float Radius { get; }
        public bool DirtChanged { get; }
        public bool PolishChanged { get; }

        internal SurfaceCleaningChange(CoinSurfaceContact contact, CleaningToolDefinition tool, bool dirtChanged, bool polishChanged)
            : this(contact, tool.ToolId, tool.BrushRadius, dirtChanged, polishChanged) { }

        private SurfaceCleaningChange(CoinSurfaceContact contact, CleaningToolId tool, float radius, bool dirtChanged, bool polishChanged)
        {
            Contact = contact; Tool = tool; Radius = radius;
            DirtChanged = dirtChanged; PolishChanged = polishChanged;
        }

        internal SurfaceCleaningChange WithFlags(bool dirtChanged, bool polishChanged) =>
            new(Contact, Tool, Radius, dirtChanged, polishChanged);
    }
}
