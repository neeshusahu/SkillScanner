
public interface IMapper<T>
{
    T Map(Dictionary<string, object> source, Dictionary<string, string> mapping);
}