namespace BinanceAnalysis.Console.Models
{
    public class MonitoredSymbolDto
    {
        public string Symbol { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}