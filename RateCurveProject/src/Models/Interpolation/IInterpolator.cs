namespace RateCurveProject.Models.Interpolation;

using RateCurveProject.Models;

public interface IInterpolator
{
    void Build(IReadOnlyList<CurvePoint> points);
    double Eval(double t);
}
