


public class ReflectionMapper<T> : IMapper<T>
    where T : new()
{
    public T Map(
        Dictionary<string, object> source,
        Dictionary<string, string> mapping)
    {
        T result = new();

        foreach (var item in source)
        {
            if (!mapping.TryGetValue(item.Key, out var propertyName))
                continue;

            var property = typeof(T)
                .GetProperty(propertyName);

            if (property == null || !property.CanWrite)
                continue;

            property.SetValue(
                result,
                ConvertValue(item.Value, property.PropertyType));
        }

        return result;
    }


    private object? ConvertValue(
        object value,
        Type targetType)
    {
        if (targetType == typeof(string))
            return value.ToString();

        return value;
    }
}