namespace Eobim.RevitApi.Framework;

public interface IDto
{
	List<(string, object)> ToObservableObject();
}

public abstract class Dto: IDto
{
    public List<(string, object)> ToObservableObject()
    {
        return DtoFormatter.FormatAsObject(this);
    }
}