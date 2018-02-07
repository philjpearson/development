//
//	Last mod:	07 February 2018 17:14:17
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
	public partial class MainWindow : WindowBase
		{
		private YouTubeService youtubeService;

		public MainWindow()
			{
			InitializeComponent();
			DataContext = this;
			Loaded += MainWindow_Loaded;
			}

		private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
			{
			var result = await Login();
			if (result.Success)
				{
				GetVideoInformation();
				}
			else
				{
				MessageBox.Show($"Login failed: {result.Error}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
				}
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
			public string Privacy { get; set; }
			}

		class Details
			{
			public ulong? ViewCount { get; set; }
			public DateTime? RecordingDate { get; set; }
			public string Privacy { get; set; }

			public ulong ViewCountToGo { get { return ViewCount ?? 0; } }

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

		private async Task<Result> Login()
			{
			Result result;

			using (var jsonStreamReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoManager.Assets.client_id.json")))
				{
				string json = jsonStreamReader.ReadToEnd();
				OAuth2Interface oAuth = new OAuth2Interface();
				result = oAuth.InitialiseClient(json);
				if (result.Success
					&& (await oAuth.Authorise(new string[] { YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.YoutubeForceSsl })).Success)
					{
					youtubeService = new YouTubeService(new BaseClientService.Initializer()
						{
						HttpClientInitializer = oAuth.Credential,
						ApplicationName = GetType().ToString()
						});
					}
				}
			return result;
			}

		private async void GetVideoInformation()
			{
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
					var listReq = youtubeService.Videos.List("recordingDetails,statistics,status");
					listReq.Id = string.Join(",", ids);
					listReq.Fields = "items(recordingDetails/recordingDate,statistics/viewCount,status/privacyStatus)";
					var listResponse = await listReq.ExecuteAsync();
					var details = (from v in listResponse.Items select new Details { ViewCount = v.Statistics.ViewCount, RecordingDate = v.RecordingDetails?.RecordingDate, Privacy=v.Status.PrivacyStatus }).ToList();

					// zip the two sets of results together into the list of video information
					videos.AddRange(vlist.Zip(details, (v, d) => { v.ViewCount = d.ViewCountToGo; v.RecordingDate = d.RecordingDateToGo; v.Privacy = d.Privacy; return v; }));

					if (response.NextPageToken != searchReq.PageToken)
						searchReq.PageToken = response.NextPageToken;
					else
						response.NextPageToken = null;  // there seems to be a bug that stops it returning null after a non-null
					}
				while (response.NextPageToken != null && response.Items.Count == searchReq.MaxResults);
				Result.SetSuccess();
				}
			catch (Exception ex)
				{
				Result.SetError(ex.Message);
				}
			Videos = videos;
			}

		async Task MakeLiveEventAsync()
			{
			Result result = new Result();
			bool makeOne = false;

			try
				{
				List<LiveBroadcast> broadcasts = new List<LiveBroadcast>();

				var listReq = youtubeService.LiveBroadcasts.List("id,snippet,contentDetails,status");
				listReq.Mine = true;
				listReq.MaxResults = 50;
				var listResponse = await listReq.ExecuteAsync();

				var streamsReq = youtubeService.LiveStreams.List("id,snippet,cdn,status");
				streamsReq.Mine = true;
				streamsReq.MaxResults = 50;
				var streamsResp = await streamsReq.ExecuteAsync();

				if (makeOne)
					{
					LiveBroadcast broadcast = new LiveBroadcast
						{
						ContentDetails = new LiveBroadcastContentDetails
							{
							EnableDvr = true,
							EnableEmbed = true,
							LatencyPreference = "normal"
							},
						Snippet = new LiveBroadcastSnippet
							{
							ScheduledStartTime = new DateTime(2018, 02, 06, 22, 23, 24),
							Title = "Stafford Live Test",
							Description = "A test event created by Phil's VideoManager application",
							LiveChatId = null
							},
						Status = new LiveBroadcastStatus
							{
							PrivacyStatus = "unlisted"
							}
						};

					var insertReq = youtubeService.LiveBroadcasts.Insert(broadcast, "id,snippet,contentDetails,status");
					broadcast = await insertReq.ExecuteAsync();
					}
				result.SetSuccess();
				}
			catch (Exception ex)
				{
				result.SetError(ex.Message);
				}
			}

		private void ShowUploadUI()
			{
			var uploadView = new UploadView(youtubeService);
			uploadView.ShowDialog();
			}

		private RelayCommand refreshCommand;

		public RelayCommand RefreshCommand
			{
			get
				{
				return refreshCommand ?? (refreshCommand = new RelayCommand(param =>
				{
					GetVideoInformation();
				},
				param=> { return youtubeService != null; }));
				}
			}

		private RelayCommand uploadCommand;

		public RelayCommand UploadCommand
			{
			get
				{
				return uploadCommand ?? (uploadCommand = new RelayCommand(param =>
				{
					ShowUploadUI();
				},
				param => { return youtubeService != null; }));
				}
			}

		private RelayCommand createBroadcastCommand;

		public RelayCommand CreateBroadcastCommand
			{
			get
				{
				return createBroadcastCommand ?? (createBroadcastCommand = new RelayCommand(async param =>
				{
					await MakeLiveEventAsync();
				},
				param => { return youtubeService != null; }));
				}
			}

		private RelayCommand exitCommand;

		public RelayCommand ExitCommand
			{
			get
				{
				return exitCommand ?? (exitCommand = new RelayCommand(param =>
				{
					Close();
				}));
				}
			}
		}
	}
