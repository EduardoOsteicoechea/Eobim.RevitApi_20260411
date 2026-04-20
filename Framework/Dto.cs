namespace Eobim.RevitApi.Framework;

public interface IDto
{
	List<(string, object)> ToObservableObject();
}

public interface Dto
{
    public List<(string, object)> ToObservableObject()
    {
        return DtoFormatter.FormatAsObject(this);
    }
}