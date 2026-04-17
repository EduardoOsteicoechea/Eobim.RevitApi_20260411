namespace Eobim.RevitApi.Framework;

public interface IDto
{
	List<(string, object)> ToObservableObject();
}