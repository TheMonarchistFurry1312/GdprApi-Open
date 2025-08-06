using System.Text.Json;

namespace GdprServices.DataExporter
{
    public class DataFormatter : IDataFormatter
    {
        public string FormatAsJson<T>(T data)
        {
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        public string FormatAsCsv<T>(T data)
        {
            if (data == null)
                return string.Empty;

            var properties = typeof(T).GetProperties();
            var header = string.Join(",", properties.Select(p => p.Name));
            var values = string.Join(",", properties.Select(p => $"\"{p.GetValue(data)?.ToString()?.Replace("\"", "\"\"") ?? ""}\""));
            return $"{header}\n{values}";
        }
    }
}
