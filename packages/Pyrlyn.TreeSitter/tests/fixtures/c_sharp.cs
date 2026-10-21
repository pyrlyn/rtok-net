using System;

namespace Demo
{
    public interface IShape
    {
        double Area();
    }

    public class Circle : IShape
    {
        public double Radius { get; }

        public double Area() => Math.PI * Radius * Radius;
    }
}
