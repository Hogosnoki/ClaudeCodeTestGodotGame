namespace ProcGen.Engine.Generation
{
    /// <summary>
    /// A creator-defined rectangular region of generation space: any X/Y offset, any size, at a
    /// given Transformation slice. Not fixed to an origin or a bounded world size.
    /// </summary>
    public readonly struct RegionSpec
    {
        public int OriginX { get; }
        public int OriginY { get; }
        public int Width { get; }
        public int Height { get; }
        public double Transformation { get; }

        public RegionSpec(int originX, int originY, int width, int height, double transformation)
        {
            OriginX = originX;
            OriginY = originY;
            Width = width;
            Height = height;
            Transformation = transformation;
        }
    }
}
