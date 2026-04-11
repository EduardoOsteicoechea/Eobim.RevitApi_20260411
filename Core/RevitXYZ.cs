using Autodesk.Revit.DB;

namespace Eobim.RevitApi.Core;

public static class RevitXYZ
{
    public static List<XYZ> UniformDistancePoints
    (
        List<XYZ> orderedPoints,
        double desiredDistance = 1
    )
    {
        var orderedPointsCount = orderedPoints.Count;

        var newPoints = new List<XYZ>();

        for (int i = 0; i < orderedPointsCount - 1; i++)
        {
            var current = orderedPoints[i];
            var next = orderedPoints[i + 1];
            
            if(current.DistanceTo(next) < 1e6)
            {
                continue;   
            }

            var line = Line.CreateBound(current, next);
            var distanceBetween = line.ApproximateLength;
            var numberOfPointsMissing = Math.Round(distanceBetween / desiredDistance);

            if (numberOfPointsMissing >= 1)
            {
                var requiredDisplacement = desiredDistance;
                for (int j = 0; j < numberOfPointsMissing; j++)
                {
                    var newPoint = current + (requiredDisplacement * line.Direction);
                    newPoints.Add(newPoint);
                    requiredDisplacement = desiredDistance * (j + 2);
                }
            }
        }

        return [..orderedPoints.Concat(newPoints)];
    }
}