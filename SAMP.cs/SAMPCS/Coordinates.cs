namespace SAMPCS
{
    public struct Coordinates
    {
        public double X;
        public double Y;
        public double Z;

        public static Coordinates operator -(Coordinates a, Coordinates b)
        {
            return new Coordinates()
            {
                X = a.X - b.X,
                Y = a.Y - b.Y,
                Z = a.Z - b.Z
            };
        }
    }
}
