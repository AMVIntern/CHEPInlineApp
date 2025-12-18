using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.MetadataExporter.Services
{
    public class ImageCaptureCsvWriter
    {
        private readonly string _csvDirectory;
        private readonly object _lockObject = new object();

        public ImageCaptureCsvWriter()
        {
            _csvDirectory = @"C:\AMV\InfeedReport";
            Directory.CreateDirectory(_csvDirectory);
        }

        public async Task WriteImageCaptureAsync(string imagePath, long timestamp, string tagId = "tag123")
        {
            try
            {
                var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime();
                string csvFileName = GetCsvFileName(date);
                string csvFilePath = Path.Combine(_csvDirectory, csvFileName);

                // Create CSV entry
                string csvLine = FormatCsvLine(imagePath, timestamp, date, tagId);

                // Append to CSV file (thread-safe)
                await Task.Run(() =>
                {
                    lock (_lockObject)
                    {
                        bool fileExists = File.Exists(csvFilePath);
                        
                        using (var writer = new StreamWriter(csvFilePath, append: true, Encoding.UTF8))
                        {
                            // Write header if file is new
                            if (!fileExists)
                            {
                                writer.WriteLine("DateTime,ImagePath,TagID");
                            }
                            
                            writer.WriteLine(csvLine);
                        }
                    }
                });

                // Log success (using Console.WriteLine since AppLogger might not be available)
                Console.WriteLine($"[ImageCaptureCSVWriter] Image capture logged to CSV: {csvFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ImageCaptureCSVWriter] Failed to write image capture to CSV: {ex.Message}");
                Console.WriteLine($"[ImageCaptureCSVWriter] Exception: {ex}");
            }
        }

        private string GetCsvFileName(DateTimeOffset date)
        {
            // Format: Infeed_Report_18-DEC-2025.csv
            string day = date.Day.ToString(); // No leading zero
            string month = date.ToString("MMM", CultureInfo.InvariantCulture).ToUpper();
            string year = date.Year.ToString();
            
            return $"Infeed_Report_{day}-{month}-{year}.csv";
        }

        private string FormatCsvLine(string imagePath, long timestamp, DateTimeOffset date, string tagId)
        {
            // Format: DateTime,ImagePath,TagID
            string dateTimeString = date.ToString("yyyy-MM-dd HH:mm:ss.fff");
            
            // Escape commas and quotes in image path if needed
            string escapedPath = EscapeCsvField(imagePath);
            string escapedTagId = EscapeCsvField(tagId);
            
            return $"{dateTimeString},{escapedPath},{escapedTagId}";
        }

        private string EscapeCsvField(string field)
        {
            // If field contains comma, quote, or newline, wrap in quotes and escape quotes
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }
    }
}
