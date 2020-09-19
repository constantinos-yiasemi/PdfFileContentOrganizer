using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;


namespace PdfOrganizer
{
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
