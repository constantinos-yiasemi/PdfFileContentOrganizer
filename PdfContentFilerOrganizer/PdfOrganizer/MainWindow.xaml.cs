using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace PdfOrganizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _pdfSaveDirectory;
        private string _pdfLookupDirectory;
        private string[] _pdfKeywords;
        private List<SearchingKeywords> _searhingKeywords;
        private bool _isAutomaticRunEnabled;
        private int _runIntervalInMinutes;
        private ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            _logger = Log.Logger = new LoggerConfiguration()
                                           .MinimumLevel.Debug()
                                           .WriteTo.Console()
                                           .WriteTo.File("logs.txt")
                                           .WriteTo.Sink(new InMemorySink(LbDebugData))
                                           .CreateLogger();

            _logger.Information("Getting values from configuration {pdf_directory_searching_location}", _pdfLookupDirectory);
            _pdfLookupDirectory = ConfigurationManager.AppSettings["pdf_directory_searching_location"];
            LblScanFolder.Content = _pdfLookupDirectory;

            _logger.Information("Getting values from configuration {pdf_directory_saving_location}", _pdfSaveDirectory);
            _pdfSaveDirectory = ConfigurationManager.AppSettings["pdf_directory_saving_location"];
            LblDestinationFolder.Content = _pdfSaveDirectory;


            _logger.Information("Checking if directory {directory} exists and if not create it. ", _pdfSaveDirectory);
            Directory.CreateDirectory(_pdfSaveDirectory);

            _pdfKeywords = ConfigurationManager.AppSettings["pdf_keywords_seperated_by_comma_join_by_hash"].Split(',');

            _searhingKeywords = new List<SearchingKeywords>();
            foreach (var keywordPair in _pdfKeywords)
            {
                var keywords = keywordPair.Split('#');
                var searchingkeys = new SearchingKeywords();
                foreach (var keyword in keywords)
                {
                    searchingkeys.Keywords.Add(keyword, false);
                }
                _searhingKeywords.Add(searchingkeys);

            }

            _logger.Information("Getting values from configuration {pdf_keywords_seperated_by_comma_join_by_hash}", string.Join(',', _pdfKeywords));


            var checkValuesIntervals = ConfigurationManager.AppSettings["check_interval_values_minutes"].Split(',');
            _logger.Information("Getting values from configuration {check_interval_values_minutes}", checkValuesIntervals);


            foreach (var value in checkValuesIntervals)
            {
                CbMinutes.Items.Add(value);
            }

        }

        private void BtnFolderScan_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (dialog.ShowDialog(this).GetValueOrDefault())
            {
                _pdfLookupDirectory = dialog.SelectedPath;
            }
        }
        private void BtnFolderDestination_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (dialog.ShowDialog(this).GetValueOrDefault())
            {
                _pdfSaveDirectory = dialog.SelectedPath;
            }
        }

        private void CheckBoxAutomatiIntervalsEnable_Checked(object sender, RoutedEventArgs e)
        {
            _isAutomaticRunEnabled = true;
            CbMinutes.IsEnabled = true;
        }

        private void CheckBoxAutomatiIntervalsEnable_Unchecked(object sender, RoutedEventArgs e)
        {
            _isAutomaticRunEnabled = false;
            CbMinutes.IsEnabled = false;
        }

        private async void BtnStartOrganizing_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            do
            {
                BtnStartOrganizing.IsEnabled = false;
                BtnStop.Visibility = Visibility.Visible;
                BtnStartOrganizing.Content = "Organizing . . .";
                try
                {


                    await Task.Run(() =>
                    {
                        var directories = Directory.GetDirectories(_pdfLookupDirectory);

                        var timer = new Stopwatch();
                        timer.Start();
                        var totalPdfFiles = 0;
                        var foundPdfFiles = 0;
                        foreach (var directory in directories)
                        {


                            _logger.Information("- Looking in directory {directory}", directory);
                            foreach (var file in Directory.GetFiles(directory).Where(f => f.EndsWith(".pdf")))
                            {
                                totalPdfFiles++;
                                foreach (var searchingWords in _searhingKeywords)
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

                                _logger.Information("- - Looking in file {file}", System.IO.Path.GetFileName(file));
                                PdfReader reader = new PdfReader(file);
                                string[] words;
                                string line;
                                PdfDocument pdfDoc = new PdfDocument(reader);
                                var text = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(1), new LocationTextExtractionStrategy());

                                words = text.Split('\n');
                                for (int j = 0, len = words.Length; j < len; j++)
                                {
                                    line = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(words[j]));
                                    foreach (var searchingWords in _searhingKeywords)
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
                                if (_searhingKeywords.Any(s => s.Keywords.All(d => d.Value == true)))
                                {
                                    foundPdfFiles++;
                                    var filenameFound = System.IO.Path.GetFileName(file);
                                    _logger.Information("- - *** Found in file {file}", filenameFound);
                                    var valueToAdd = $"{DateTime.Now.ToString("HH:mm:ss")} Found file - {filenameFound}";
                                    Application.Current.Dispatcher.Invoke(new Action(() =>
                                    {
                                        LbOutputData.Items.Add(valueToAdd);
                                        LbOutputData.ScrollIntoView(valueToAdd);
                                    }));

                                    foreach (var keywordFound in _searhingKeywords.Where(s => s.Keywords.All(d => d.Value == true)).Select(s => s.Keywords))
                                    {
                                        foreach (var k in keywordFound)
                                        {
                                            _logger.Information("- - - *** Values found  {valuesFound}", k.Key);
                                        }

                                    }

                                    var directoryName = directory.Split("\\").Last();

                                    Directory.CreateDirectory(System.IO.Path.Combine(_pdfSaveDirectory, directoryName));

                                    var fileName = System.IO.Path.GetFileName(file);

                                    var fileDestinationPath = System.IO.Path.Combine(_pdfSaveDirectory, directoryName, fileName);

                                    if (!File.Exists(fileDestinationPath))
                                        File.Copy(file, fileDestinationPath);
                                    else
                                        _logger.Warning("- - ### [ALREADY EXISTS] file {file} in {directory}", filenameFound, fileDestinationPath);

                                    _logger.Information("- - *** Copied file {file} in {directory}", filenameFound, fileDestinationPath);
                                }
                            }
                        }
                        timer.Stop();
                        _logger.Information("# Total searching time {searchTime} ms", timer.ElapsedMilliseconds);
                        _logger.Information("# Total pdf found {totalPdfFound}/{totalPdfFiles}", foundPdfFiles, totalPdfFiles);
                        if (_isAutomaticRunEnabled)
                            _logger.Information("# Done, next round in {minutes} minutes", _runIntervalInMinutes);
                        timer.Reset();
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error while organizing files.");
                }
                finally
                {
                    if (_isAutomaticRunEnabled == false)
                    {
                        BtnStartOrganizing.IsEnabled = true;
                        BtnStop.Visibility = Visibility.Hidden;
                    }

                    BtnStartOrganizing.Content = "Done!!";
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    if (_isAutomaticRunEnabled == false)
                        BtnStartOrganizing.Content = "Start Organizing";
                    else
                        BtnStartOrganizing.Content = $"Next run at {DateTime.Now.AddMinutes(_runIntervalInMinutes).ToString("HH:mm:ss")}";
                }
                if (_isAutomaticRunEnabled)
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(_runIntervalInMinutes), _cancellationTokenSource.Token);
                    }
                    catch (TaskCanceledException ex)
                    {
                        BtnStartOrganizing.IsEnabled = true;
                        BtnStop.Visibility = Visibility.Hidden;
                        BtnStartOrganizing.Content = "Start Organizing";
                        _logger.Warning("Organizing Cancelled from user.");
                    }
            }
            while (_isAutomaticRunEnabled && _runIntervalInMinutes > 0 && _cancellationTokenSource.Token.IsCancellationRequested == false);
        }

        private void CbMinutes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _runIntervalInMinutes = Convert.ToInt32(e.AddedItems[0]);
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource.Cancel();
        }

        private void BtnShowDetails_Click(object sender, RoutedEventArgs e)
        {
            if (LbDebugData.Visibility != Visibility.Visible)
            {
                LbDebugData.Visibility = Visibility.Visible;
                LbOutputData.Visibility = Visibility.Visible;
                BtnShowDetails.Content = "Hide details";
            }
            else
            {
                LbDebugData.Visibility = Visibility.Hidden;
                LbOutputData.Visibility = Visibility.Hidden;
                BtnShowDetails.Content = "Show details";
            }

        }
    }

    public class InMemorySink : ILogEventSink
    {
        readonly ITextFormatter _textFormatter = new MessageTemplateTextFormatter("{Timestamp} [{Level}] {Message}{Exception}");
        private ListBox _debugOutput;

        public ConcurrentQueue<string> Events { get; } = new ConcurrentQueue<string>();
        public InMemorySink(ListBox debugOutput)
        {
            _debugOutput = debugOutput;
        }
        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            var renderSpace = new StringWriter();
            _textFormatter.Format(logEvent, renderSpace);
            //            Events.Enqueue(renderSpace.ToString());
            var valueToAdd = renderSpace.ToString();
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                _debugOutput.Items.Add(valueToAdd);
                _debugOutput.ScrollIntoView(valueToAdd);
            }));
        }
    }

}
