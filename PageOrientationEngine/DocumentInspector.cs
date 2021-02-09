using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using PageOrientationEngine.Helpers;

using Tesseract;

namespace PageOrientationEngine
{
    #region DocumentInspectorPageOrientation
    /// <summary>
    /// The orientation of the text on a page
    /// </summary>
    public enum DocumentInspectorPageOrientation
    {
        /// <summary>
        /// The text on page is correctly orientated
        /// </summary>
        PageCorrect,

        /// <summary>
        /// The text on the page is rotated to the right
        /// </summary>
        PageRotatedRight,

        /// <summary>
        /// The text on the page is upside down
        /// </summary>
        PageUpsideDown,

        /// <summary>
        /// The text on the page is rotated to the left
        /// </summary>
        PageRotatedLeft,

        /// <summary>
        /// The quality of text on the page is to bad to determine the orientation of the page
        /// </summary>
        Undetectable
    }
    #endregion

    public class DocumentInspector
    {
        #region Internal class WorkQueueItem
        /// <summary>
        /// Used to track all the workqueue items
        /// </summary>
        private class WorkQueueItem
        {
            /// <summary>
            /// The number of the page
            /// </summary>
            public int PageNumber { get; private set; }

            /// <summary>
            /// The page as a memory stream
            /// </summary>
            public MemoryStream MemoryStream { get; private set; }

            /// <summary>
            /// Creates this object
            /// </summary>
            /// <param name="pageNumber">The number of the page</param>
            /// <param name="memoryStream">The page as a memory stream</param>
            public WorkQueueItem(int pageNumber, MemoryStream memoryStream)
            {
                PageNumber = pageNumber;
                MemoryStream = memoryStream;
            }
        }
        #endregion

        #region Fields
        /// <summary>
        /// The load queue
        /// </summary>
        ConcurrentQueue<WorkQueueItem> _workQueue;

        /// <summary>
        /// Contains all the result of the page orientation detection
        /// </summary>
        private Dictionary<int, DocumentInspectorPageOrientation> _detectionResult;
        #endregion

        #region Properties
        /// <summary>
        /// The path to the Tesseract data files
        /// </summary>
        public string TesseractDataPath { get; private set; }

        /// <summary>
        /// The language that needs to be used by Tesseract
        /// </summary>
        public string TesseractLanguage { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates this object
        /// </summary>
        /// <param name="dataPath">The path to the Tesseract language files</param>
        /// <param name="language">The Tesseract languag to use, e.g. eng</param>
        public DocumentInspector(string dataPath, string language)
        {
            TesseractDataPath = dataPath;
            TesseractLanguage = language;
        }
        #endregion

        #region ProcessWorkQueue
        /// <summary>
        /// Takes one item out of the <see cref="_workQueue"/>, processes it and returns the output 
        /// in the <see cref="_detectionResult"/> dictionary
        /// </summary>
        private void ProcessWorkQueue()
        {
            while (_workQueue.TryDequeue(out WorkQueueItem workQueueItem))
                using (var bitmap = Image.FromStream(workQueueItem.MemoryStream) as Bitmap)
                    _detectionResult.Add(workQueueItem.PageNumber, DetectPageOrientation(bitmap));
        }
        #endregion

        #region DetectPageOrientation
        /// <summary>
        /// Returns a list with <see cref="DocumentInspectorPageOrientation">DocumentInspectorPageOrientations</see>
        /// according to the amount of <paramref name="memoryStreams"/>
        /// </summary>
        /// <param name="memoryStreams"></param>
        /// <returns></returns>
        public Dictionary<int, DocumentInspectorPageOrientation> DetectPageOrientation(List<MemoryStream> memoryStreams)
        {
            _detectionResult = new Dictionary<int, DocumentInspectorPageOrientation>();
            _workQueue = new ConcurrentQueue<WorkQueueItem>();

            // WorkQueue vullen
            var i = 1;
            foreach (var memoryStream in memoryStreams)
            {
                _workQueue.Enqueue(new WorkQueueItem(i, memoryStream));
                i++;
            }

            var procCount = Environment.ProcessorCount;
            //var procCount = 4;

            if (_workQueue.Count < procCount)
                procCount = _workQueue.Count;

            var threads = new Thread[procCount];

            // Spawn threads
            for (i = 0; i < procCount; i++)
            {
                ThreadStart threadStart = ProcessWorkQueue;
                threads[i] = new Thread(threadStart);
                threads[i].Start();
            }

            var workDone = false;

            while (!workDone)
            {
                for (i = 0; i < procCount; i++)
                    workDone = threads[i].Join(1000*60);
            }

            return _detectionResult;
        }

        /// <summary>
        /// Returns a list with <see cref="DocumentInspectorPageOrientation">DocumentInspectorPageOrientations</see>
        /// according to the amount of "pages" in the <paramref name="inputFile"/>
        /// </summary>
        /// <param name="inputFile">The input file</param>
        /// <returns></returns>
        public Dictionary<int, DocumentInspectorPageOrientation> DetectPageOrientation(string inputFile)
        {
            return DetectPageOrientation(TiffUtils.SplitTiffImage(inputFile)).OrderBy(d => d.Key).ToDictionary(k => k.Key, v => v.Value);
        }

        /// <summary>
        /// Returns the <see cref="DocumentInspectorPageOrientation"/> of the <paramref name="bitmap"/> 
        /// according to the text that is on it
        /// </summary>
        /// <param name="bitmap">The bitmap with text</param>
        /// <returns></returns>
        public DocumentInspectorPageOrientation DetectPageOrientation(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new NullReferenceException("The bitmap parameter is not set");

            if (bitmap.PixelFormat == PixelFormat.Format1bppIndexed)
                bitmap = BitmapUtils.CopyToBpp(bitmap, 1);

            using (var engine = new TesseractEngine(TesseractDataPath, TesseractLanguage))
            using (var image = PixConverter.ToPix(bitmap))
            using (var page = engine.Process(image, PageSegMode.AutoOsd))
                return (DocumentInspectorPageOrientation)(int)page.AnalyseLayout().GetProperties().Orientation;
        }
        #endregion
    }
}
