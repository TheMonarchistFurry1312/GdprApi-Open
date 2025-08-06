namespace GdprServices.DataExporter
{
    public interface IDataFormatter
    {
        string FormatAsJson<T>(T data);
        string FormatAsCsv<T>(T data);
    }
}
