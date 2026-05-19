using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public record Face_FromDirectShapeArgs(DirectShape DirectShape, string FaceToObtain);

public class Face_FromDirectShape : MultistepObservableAction<Face_FromDirectShapeArgs, Face_FromDirectShapeDto, Face>
{
    public Face_FromDirectShape(Document doc, string workflowName) : base(doc, workflowName) { }

    public override void SafelyInitializeInputs(Face_FromDirectShapeArgs args)
    {
        _dto.DirectShape = args.DirectShape;
        _dto.FaceToObtain = args.FaceToObtain;
    }

    protected override void SetActions()
    {
        Add(GetFaces);
        Add(GetInterestFace);
        Add(SetResult);
    }

    public void GetFaces(List<string> _stateTrace)
    {
        var result = new List<Face>();

        // CRITICAL: ComputeReferences must be true to use the face later
        var options = new Options { ComputeReferences = true };
        var geometry = _dto.DirectShape.get_Geometry(options);

        if (geometry != null)
        {
            ExtractFacesFromGeometry(geometry, result);
        }

        _dto.Faces = result;
        _stateTrace.Add($"Extracted {_dto.Faces.Count} faces.");
    }

    // Helper method to unwrap GeometryInstances
    private void ExtractFacesFromGeometry(GeometryElement geomElem, List<Face> faceList)
    {
        foreach (var geoObj in geomElem)
        {
            if (geoObj is Solid solid && solid.Faces.Size > 0)
            {
                foreach (Face face in solid.Faces)
                {
                    faceList.Add(face);
                }
            }
            else if (geoObj is GeometryInstance geomInstance)
            {
                // Unwrap the instance geometry and recurse
                var instanceGeom = geomInstance.GetInstanceGeometry();
                if (instanceGeom != null)
                {
                    ExtractFacesFromGeometry(instanceGeom, faceList);
                }
            }
        }
    }

    public void GetInterestFace(List<string> _stateTrace)
    {
        Face result = null;

        if (_dto.FaceToObtain.ToLower() == "top")
        {
            result = GetHighestTopFaceByNormal(_dto.Faces);
        }

        if (result == null)
        {
            throw new InvalidOperationException($"Could not find a '{_dto.FaceToObtain}' face on DirectShape {_dto.DirectShape.Id}.");
        }

        _dto.ObtainedFace = result;
    }

    public Face GetHighestTopFaceByNormal(List<Face> faces)
    {
        Face topFace = null;
        double maxZ = double.MinValue;

        foreach (var face in faces)
        {
            // We are looking for flat faces pointing up
            if (face is PlanarFace planarFace)
            {
                // Check if the normal is pointing straight up (Z = 1)
                if (planarFace.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                {
                    // If there are multiple flat top faces (like steps), get the highest one
                    if (planarFace.Origin.Z > maxZ)
                    {
                        maxZ = planarFace.Origin.Z;
                        topFace = face;
                    }
                }
            }
        }

        return topFace;
    }

    public void SetResult(List<string> _stateTrace)
    {
        Result = _dto.ObtainedFace;
    }
}

public class Face_FromDirectShapeDto : Dto
{
    public DirectShape DirectShape { get; set; }
    public string FaceToObtain { get; set; }
    public List<Face> Faces { get; set; }
    public Face ObtainedFace { get; set; }
}