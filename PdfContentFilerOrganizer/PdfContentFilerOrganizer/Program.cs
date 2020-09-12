using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfContentFilerOrganizer
{
    class Program
    {
        static async Task Main(string[] args)
        {

            var logger = Log.Logger = new LoggerConfiguration()
                                            .MinimumLevel.Debug()
                                            .WriteTo.Console()
                                            .WriteTo.File("logs.txt")
                                            .CreateLogger();

            var pdfLookupDirectory = ConfigurationManager.AppSettings["pdf_directory_searching_location"];
            logger.Information("Getting values from configuration {pdf_directory_searching_location}", pdfLookupDirectory);

            var pdfSaveDirectory = ConfigurationManager.AppSettings["pdf_directory_saving_location"];
            logger.Information("Getting values from configuration {pdf_directory_saving_location}", pdfSaveDirectory);
            logger.Information("Checking if directory {directory} exists and if not create it. ", pdfSaveDirectory);
            Directory.CreateDirectory(pdfSaveDirectory);

            var pdfKeywords = ConfigurationManager.AppSettings["pdf_keywords_seperated_by_comma_join_by_hash"].Split(',');
            var searhingKeywords = new List<SearchingKeywords>();
            foreach (var keywordPair in pdfKeywords)
            {
                var keywords = keywordPair.Split('#');
                var searchingkeys = new SearchingKeywords();
                foreach (var keyword in keywords)
                {
                    searchingkeys.Keywords.Add(keyword, false);
                }
                searhingKeywords.Add(searchingkeys);

            }

            logger.Information("Getting values from configuration {pdf_keywords_seperated_by_comma_join_by_hash}", string.Join(',', pdfKeywords));

            var newPdfCheckIntervalMinutes = ConfigurationManager.AppSettings["check_for_new_pdf_interval_minutes"];
            logger.Information("Getting values from configuration {check_for_new_pdf_interval_minutes}", newPdfCheckIntervalMinutes);


            var directories = Directory.GetDirectories(pdfLookupDirectory);

            var timer = new Stopwatch();


            while (true)
            {
                timer.Start();
                var totalPdfFiles = 0;
                var foundPdfFiles = 0;
                foreach (var directory in directories)
                {


                    logger.Information("- Looking in directory {directory}", directory);
                    foreach (var file in Directory.GetFiles(directory).Where(f => f.EndsWith(".pdf")))
                    {
                        totalPdfFiles++;
                        foreach (var searchingWords in searhingKeywords)
                        {
                            var keysToMakeFalse = new List<string>();
                            foreach (var searchPair in searchingWords.Keywords)
                            {
                                keysToMakeFalse.Add(searchPair.Key);
                            }
                            foreach (var key in keysToMakeFalse)
                            {
                                searchingWords.Keywords[key] = false;
                            }
                        }

                        logger.Information("- - Looking in file {file}", Path.GetFileName(file));
                        PdfReader reader = new PdfReader(file);
                        string[] words;
                        string line;
                        PdfDocument pdfDoc = new PdfDocument(reader);
                        var text = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(1), new LocationTextExtractionStrategy());

                        words = text.Split('\n');
                        for (int j = 0, len = words.Length; j < len; j++)
                        {
                            line = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(words[j]));
                            foreach (var searchingWords in searhingKeywords)
                            {
                                var keysToMakeTrue = new List<string>();
                                foreach (var searchPair in searchingWords.Keywords)
                                {
                                    if (line.ToLower().Contains(searchPair.Key.ToLower()))
                                    {
                                        keysToMakeTrue.Add(searchPair.Key);
                                    }
                                }
                                foreach (var key in keysToMakeTrue)
                                {
                                    searchingWords.Keywords[key] = true;
                                }
                            }
                        }
                        if (searhingKeywords.Any(s => s.Keywords.All(d => d.Value == true)))
                        {
                            foundPdfFiles++;
                            logger.Information("- - *** Found in file {file}", Path.GetFileName(file));

                            foreach (var keywordFound in searhingKeywords.Where(s => s.Keywords.All(d => d.Value == true)).Select(s => s.Keywords))
                            {
                                foreach (var k in keywordFound)
                                {
                                    logger.Information("- - - *** Values found  {valuesFound}", k.Key);
                                }

                            }

                            var directoryName = directory.Split("\\").Last();

                            Directory.CreateDirectory(Path.Combine(pdfSaveDirectory, directoryName));

                            var fileName = Path.GetFileName(file);

                            var fileDestinationPath = Path.Combine(pdfSaveDirectory, directoryName, fileName);

                            if (!File.Exists(fileDestinationPath))
                                File.Copy(file, fileDestinationPath);
                            else
                                logger.Warning("- - ### [ALREADY EXISTS] file {file} in {directory}", Path.GetFileName(file), fileDestinationPath);

                            logger.Information("- - *** Copied file {file} in {directory}", Path.GetFileName(file), fileDestinationPath);
                        }
                    }
                }
                timer.Stop();
                logger.Information("# Total searching time {searchTime} ms", timer.ElapsedMilliseconds);
                logger.Information("# Total pdf found {totalPdfFound}/{totalPdfFiles}", foundPdfFiles, totalPdfFiles);
                logger.Information("# Done, next round in {minutes} minutes", newPdfCheckIntervalMinutes);
                timer.Reset();

                await Task.Delay(TimeSpan.FromMinutes(Convert.ToInt32(newPdfCheckIntervalMinutes)));
            }

        }

    }
    public class SearchingKeywords
    {
        public Dictionary<string, bool> Keywords = new Dictionary<string, bool>();
    }

}


