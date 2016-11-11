#r "System.Runtime"
#r "System.Threading.Tasks"
#r "System.IO"

using System;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

public async static void Run(CloudBlockBlob myBlob, CloudTable table, TraceWriter log)
{
    string subscriptionKey = "[application-key]";
    var emotionSC = new EmotionServiceClient(subscriptionKey);
    log.Verbose($"C# Blob trigger function processed: {myBlob}");
    log.Info("myBlob:StorageUri.PrimaryUri="+myBlob.StorageUri.PrimaryUri.AbsoluteUri);
    log.Info("myBlob:Name="+myBlob.Name);
 
    try{
        Microsoft.ProjectOxford.Emotion.Contract.Emotion[] emotionsResult = null;
        using (var memoryStream = new MemoryStream())
        {
            await myBlob.DownloadToStreamAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            emotionsResult = await emotionSC.RecognizeAsync(memoryStream);
        }
        log.Info("Succeeded to call RecognizeAsync");
        double angerTotal = 0;
        double contemptTotal=0;
        double disgustTotal=0;
        double fearTotal=0;
        double happinessTotal=0;
        double neutralTotal=0;
        double sadnessTotal=0;
        double supriseTotal=0;
        int numOfPerson=emotionsResult.Length;

        string deviceId = "";
        DateTime uploadedTime;
        var regx = new System.Text.RegularExpressions.Regex(
    @"^(?<deviceId>[\w\-.]+)_(?<yyyy>[0-9]{4})(?<MM>[0-9]{2})(?<dd>[0-9]{2})_(?<hh>[0-9]{2})_(?<mm>[0-9]{2})_(?<ss>[0-9]{2})_Pro\.jpg$");
        var match = regx.Match(myBlob.Name);
        if (match.Length > 0)
        {
            deviceId = match.Groups["deviceId"].Value;
            var datetime = new string[7];
            int index = 0;
            datetime[index++] = match.Groups["yyyy"].Value;
            datetime[index++] = match.Groups["MM"].Value;
            datetime[index++] = match.Groups["dd"].Value;
            datetime[index++] = match.Groups["hh"].Value;
            datetime[index++] = match.Groups["mm"].Value;
            datetime[index++] = match.Groups["ss"].Value;
            datetime[index++] = "000";

            var datetimeInt = new int[datetime.Length];
            for (int i = 0; i < datetime.Length; i++)
            {
                if (i > 0)
                {
                    var dt = datetime[i];
                    if (datetime[i].StartsWith("0"))
                    {
                        dt = datetime[i].Substring(1);
                    }
                    datetimeInt[i] = int.Parse(dt);
                }
                else
                {
                    datetimeInt[i] = int.Parse(datetime[i]);
                }
            }

            uploadedTime = new DateTime(datetimeInt[0], datetimeInt[1], datetimeInt[2], datetimeInt[3], datetimeInt[4], datetimeInt[5], datetimeInt[6]);
        }
        else
        {
            deviceId = myBlob.Name.Substring(0, myBlob.Name.LastIndexOf("."));
            uploadedTime = DateTime.Now;
        }
        var uploadedStatus = new UploadedPhotoStatus(){
               DeviceId=deviceId,
               Time=uploadedTime,
               FileUri = myBlob.StorageUri.PrimaryUri.AbsoluteUri,
               NumOfPerson = numOfPerson
        };
        if(emotionsResult.Count()>0)
        {
            log.Info("Human Existed!");
            var index=0;
            foreach(var em in emotionsResult){
                var timestamp = uploadedTime.ToString("yyyyMMddHHmmssfff");
                var emotionScores = new EmotionScores()
                {
                    PartitionKey = deviceId,
                    RowKey = deviceId+timestamp + (index++),
                    MeasuredTime = uploadedTime,
                    MeasuredTS = timestamp,
                    DeviceId = deviceId,
                    Anger = em.Scores.Anger,
                    Contempt = em.Scores.Contempt,
                    Disgust = em.Scores.Disgust,
                    Fear = em.Scores.Fear,
                    Happiness = em.Scores.Happiness,
                    Neutral = em.Scores.Neutral,
                    Sadness = em.Scores.Sadness,
                    Surprise = em.Scores.Surprise
                };
                angerTotal+=em.Scores.Anger;
                contemptTotal+=em.Scores.Contempt;
                disgustTotal+=em.Scores.Disgust;
                fearTotal+=em.Scores.Fear;
                happinessTotal+=em.Scores.Happiness;
                neutralTotal+=em.Scores.Neutral;
                sadnessTotal+=em.Scores.Sadness;
                supriseTotal+=em.Scores.Surprise;
                var insertOp = TableOperation.Insert(emotionScores);
                table.Execute(insertOp);
                log.Info("Inserted Table - "+ emotionScores.RowKey);
                uploadedStatus.Anger = angerTotal/numOfPerson;
                uploadedStatus.Contempt = contemptTotal/numOfPerson;
                uploadedStatus.Disgust = disgustTotal/numOfPerson;
                uploadedStatus.Fear=fearTotal/numOfPerson;
                uploadedStatus.Happiness=happinessTotal/numOfPerson;
                uploadedStatus.Neutral=neutralTotal/numOfPerson;
                uploadedStatus.Sadness=sadnessTotal/numOfPerson;
                uploadedStatus.Suprise=supriseTotal/numOfPerson;
            }
        }
        else
        {
            log.Info("None of Humans exist.");
        }
        var hubConnection = new Microsoft.AspNet.SignalR.Client.HubConnection("http://[Web-App-Name].azurewebsites.net");
        var proxy = hubConnection.CreateHubProxy("EmotionPhotoHub");
        hubConnection.Start().Wait();
        proxy.Invoke("PhotoUploaded",new []{uploadedStatus        }).Wait();
        log.Info("Notified - PhotoUploaded");
    }
    catch (Exception ex){
        log.Info("Exception:"+ex.Message);
    }
}



    public class EmotionScores : TableEntity
    {
        public string DeviceId { get; set; }
        public DateTime MeasuredTime { get; set; }
        public string MeasuredTS{get;set;}
        public double Anger { get; set; }
        public double Contempt { get; set; }
        public double Disgust { get; set; }
        public double Fear { get; set; }
        public double Happiness { get; set; }
        public double Neutral { get; set; }
        public double Sadness { get; set; }
        public double Surprise { get; set; }
    }
    public class UploadedPhotoStatus
    {
        public string DeviceId { get; set; }
        public DateTime Time { get; set; }
        public string FileUri { get; set; }
        public int NumOfPerson { get; set; }
        /// <summary>
        /// いとわしさ
        /// </summary>
        public double Disgust { get; set; }
        /// <summary>
        /// 怒り
        /// </summary>
        public double Anger { get; set; }
        /// <summary>
        /// 軽蔑
        /// </summary>
        public double Contempt { get; set; }
        /// <summary>
        /// 恐れ
        /// </summary>
        public double Fear { get; set; }
        /// <summary>
        /// 幸せ
        /// </summary>
        public double Happiness { get; set; }
        /// <summary>
        /// 真顔
        /// </summary>
        public double Neutral { get; set; }
        /// <summary>
        /// 悲しみ
        /// </summary>
        public double Sadness { get; set; }
        /// <summary>
        /// 驚き
        /// </summary>
        public double Suprise { get; set; }
    }

