//
//	Last mod:	11 July 2016 11:27:51
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using VideoManager.Helpers;

namespace VideoManager
	{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
		{
		public event PropertyChangedEventHandler PropertyChanged;

		public MainWindow()
			{
			InitializeComponent();
			DataContext = this;
			Loaded += MainWindow_Loaded;
			}

		private void MainWindow_Loaded(object sender, RoutedEventArgs e)
			{
			GetVideoInformation();
			}

		//
		//  "latitude": 52.8155784607,
		//  "longitude": -2.11637997627,
		//

		public class Video
			{
			public string Id { get; set; }
			public string RecordingDate { get; set; }
			public string Title { get; set; }
			public string Description { get; set; }
			public ulong ViewCount { get; set; }
			}

		class Details
			{
			public ulong? ViewCount
				{
				get;
				set;
				}
			public DateTime? RecordingDate
				{
				get;
				set;
				}

			public ulong ViewCountToGo { get { return ViewCount.HasValue ? ViewCount.Value : 0; } }

			public string RecordingDateToGo { get { return RecordingDate.HasValue ? RecordingDate.Value.ToString("yyyy-MM-dd") : string.Empty; } }
			}

		private Result result = new Result();

		public Result Result
			{
			get { return result; }
			set
				{
				if (result != value)
					{
					result = value;
					RaisePropertyChanged(nameof(Result));
					}
				}
			}

		private List<Video> videos;

		public List<Video> Videos
			{
			get { return videos; }
			set
				{
				if (videos != value)
					{
					videos = value;
					RaisePropertyChanged(nameof(Videos));
					}
				}
			}

		private void button_Click(object sender, RoutedEventArgs e)
			{
			}

		private async void GetVideoInformation()
			{ 
			using (var jsonStreamReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoManager.Assets.client_id.json")))
				{
				string json = jsonStreamReader.ReadToEnd();
				OAuth2Interface oAuth = new OAuth2Interface();
				if (oAuth.InitialiseClient(json).Success
					&& (await oAuth.Authorise(new string[] { YouTubeService.Scope.YoutubeReadonly })).Success)
					{
					var youtubeService = new YouTubeService(new BaseClientService.Initializer()
						{
						HttpClientInitializer = oAuth.Credential,
						ApplicationName = this.GetType().ToString()
						});

					List<Video> videos = new List<Video>();
					SearchListResponse response;

					try
						{
						// searches are allowed a maximum of 50 results per call (max allowed value of MaxResults)
						var searchReq = youtubeService.Search.List("snippet");
						//					searchReq.Q = "Bible";
						searchReq.ForMine = true;
						searchReq.Type = "video";
						searchReq.Fields = "items(id/videoId,snippet(description,title)),nextPageToken";
						searchReq.MaxResults = 50;
						do
							{
							// get a page of videos, build a list of video ids with titles and descriptions
							response = await searchReq.ExecuteAsync();
							var vlist = (from v in response.Items select new Video { Id = v.Id.VideoId, Title = v.Snippet.Title, Description = v.Snippet.Description }).ToList();

							// get the view count and recording date for each of the videos
							var ids = from v in vlist select v.Id;
							var listReq = youtubeService.Videos.List("recordingDetails,statistics");
							listReq.Id = string.Join(",", ids);
							listReq.Fields = "items(recordingDetails/recordingDate,statistics/viewCount)";
							var listResponse = await listReq.ExecuteAsync();
							var details = (from v in listResponse.Items select new Details { ViewCount = v.Statistics.ViewCount, RecordingDate = v.RecordingDetails?.RecordingDate }).ToList();

							// zip the two sets of results together into the list of video information
							videos.AddRange(vlist.Zip(details, (v, d) => { v.ViewCount = d.ViewCountToGo; v.RecordingDate = d.RecordingDateToGo; return v; }));

							if (response.NextPageToken != searchReq.PageToken)
								searchReq.PageToken = response.NextPageToken;
							else
								response.NextPageToken = null;  // there seems to be a bug that stops it returning null after a non-null
							}
						while (response.NextPageToken != null && response.Items.Count == searchReq.MaxResults);
						}
					catch (Exception ex)
						{
						Result.SetError(ex.Message);
						}
					Videos = videos;
					Result.SetSuccess();
					}
				}
			}

		void RaisePropertyChanged(string propertyName)
			{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
