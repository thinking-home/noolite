using System;
using System.Net.Http;
using System.Xml.Linq;
using System.Xml.XPath;


namespace ThinkingHome.NooLite
{
	public class PR1132Gateway : IDisposable
	{
	    private const string SensorDataFile = "sens.xml";
	    private readonly HttpClient _client = new HttpClient();

		public Uri Host { get; }

		public PR1132Gateway(string host)
		{
			Host = new Uri("http://" + host);
		}
        
		public PR1132SensorData[] LoadSensorData()
		{
			var xml = _client.GetStringAsync(new Uri(Host, SensorDataFile)).Result;
			var doc = XDocument.Parse(xml);

			var result = new PR1132SensorData[4];

			for (var i = 0; i < 4; i++)
			{
				var strT = doc.XPathSelectElement("response/snst" + i).Value;
				var strH = doc.XPathSelectElement("response/snsh" + i).Value;
				var strState = doc.XPathSelectElement("response/snt" + i).Value;

				var data = new PR1132SensorData { State = (SensorState)Convert.ToInt32(strState) };

				decimal t;

				if (decimal.TryParse(strT, out t))
				{
					data.Temperature = t;
				}

				int h;

				if (int.TryParse(strH, out h))
				{
					data.Humidity = h;
				}

				result[i] = data;
			}

			return result;
		}

		public void Dispose()
		{
			_client.Dispose();
		}
	}
}
