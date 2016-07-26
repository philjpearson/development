//
//	Last mod:	26 July 2016 22:45:10
//
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Win32;
using VideoManager.Helpers;

namespace VideoManager
	{
	/// <summary>
	/// Interaction logic for UploadView.xaml
	/// </summary>
	public partial class UploadView : WindowBase
		{
		private YouTubeService youtubeService;
		private long byteCount;

		public UploadView(YouTubeService youtubeService)
			{
			this.youtubeService = youtubeService;
			InitializeComponent();
			DataContext = this;
			Loaded += Window_Loaded;
			}

		private string videoTitle;

		public string VideoTitle
			{
			get { return videoTitle; }
			set
				{
				if (videoTitle != value)
					{
					videoTitle = value;
					RaisePropertyChanged(nameof(VideoTitle));
					}
				}
			}

		private string videoDescription;

		public string VideoDescription
			{
			get { return videoDescription; }
			set
				{
				if (videoDescription != value)
					{
					videoDescription = value;
					RaisePropertyChanged(nameof(VideoDescription));
					}
				}
			}

		private string speaker;

		public string Speaker
			{
			get { return speaker; }
			set
				{
				if (speaker != value)
					{
					speaker = value;
					RaisePropertyChanged(nameof(Speaker));
					UpdateDescription();
					}
				}
			}

		private string ecclesia;

		public string Ecclesia
			{
			get { return ecclesia; }
			set
				{
				if (ecclesia != value)
					{
					ecclesia = value;
					RaisePropertyChanged(nameof(Ecclesia));
					UpdateDescription();
					}
				}
			}

		private DateTime videoDate;

		public DateTime VideoDate
			{
			get { return videoDate; }
			set
				{
				if (videoDate != value)
					{
					videoDate = value;
					RaisePropertyChanged(nameof(VideoDate));
					UpdateDescription();
					}
				}
			}

		private string videoPath;

		public string VideoPath
			{
			get { return videoPath; }
			set
				{
				if (videoPath != value)
					{
					videoPath = value;
					RaisePropertyChanged(nameof(VideoPath));
					if (string.IsNullOrWhiteSpace(VideoTitle))
						VideoTitle = Path.GetFileNameWithoutExtension(videoPath);
					}
				}
			}

		private double percentUploaded;

		public double PercentUploaded
			{
			get { return percentUploaded; }
			set
				{
				if (percentUploaded != value)
					{
					percentUploaded = value;
					RaisePropertyChanged(nameof(PercentUploaded));
					}
				}
			}

		private string videoId;

		public string VideoId
			{
			get { return videoId; }
			set
				{
				if (videoId != value)
					{
					videoId = value;
					RaisePropertyChanged(nameof(VideoId));
					}
				}
			}

		private RelayCommand browseCommand;

		public RelayCommand BrowseCommand
			{
			get
				{
				return browseCommand ?? (browseCommand = new RelayCommand(param =>
				{
					var ofd = new OpenFileDialog()
						{
						Filter = "mp4 files (*.mp4)|*.mp4|All files (*.*)|*.*",
						DefaultExt = ".mp4",
						InitialDirectory = @"E:\Videos"
						};
					if (ofd.ShowDialog(this) == true)
						{
						VideoPath = ofd.FileName;
						}
				}));
				}
			}

		private RelayCommand uploadCommand;

		public RelayCommand UploadCommand
			{
			get
				{
				return uploadCommand ?? (uploadCommand = new RelayCommand(async param =>
				{
					await Upload();
				},
				param => { return !string.IsNullOrWhiteSpace(VideoPath); }));
				}
			}

		private async Task<Result> Upload()
			{
			var result = new Result();

			var video = new Video();
			video.Snippet = new VideoSnippet()
				{
				Title = VideoTitle,
				Description = VideoDescription,
				Tags = new string[] { "tag1", "tag2" },
				CategoryId = "27" // Education. See https://developers.google.com/youtube/v3/docs/videoCategories/list
				};
			video.Status = new VideoStatus()
				{
				PrivacyStatus = "private", // "unlisted" or "private" or "public"
				Embeddable = true
				};
			video.RecordingDetails = new VideoRecordingDetails()
				{
				Location = new GeoPoint() { Latitude = 52.8155784607, Longitude = -2.11637997627 }
				};

			using (var fileStream = new FileStream(videoPath, FileMode.Open))
				{
				byteCount = fileStream.Length;
				var insertReq = youtubeService.Videos.Insert(video, "snippet,status,recordingDetails", fileStream, "video/*");
				insertReq.AutoLevels = false;
				insertReq.NotifySubscribers = false;

				insertReq.ProgressChanged += videosInsertRequest_ProgressChanged;
				insertReq.ResponseReceived += videosInsertRequest_ResponseReceived;

				await insertReq.UploadAsync();
				}
			return result;
			}

		void videosInsertRequest_ProgressChanged(IUploadProgress progress)
			{
			switch (progress.Status)
				{
			case UploadStatus.Uploading:
				Dispatcher.Invoke(() => { PercentUploaded = progress.BytesSent * 100 / byteCount; });
				break;

			case UploadStatus.Failed:
				Dispatcher.Invoke(() =>
				{
					MessageBox.Show($"An error prevented the upload from completing.\n{progress.Exception}", Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
				});
				break;
				}
			}

		void videosInsertRequest_ResponseReceived(Video video)
			{
			Dispatcher.Invoke(() => { VideoId = video.Id; });
			}

		private void Window_Loaded(object sender, RoutedEventArgs e)
			{
			Speaker = "[speaker]";
			Ecclesia = "[ecclesia]";
			VideoDate = DateTime.Now.Date;
			UpdateDescription();
			}

		private void UpdateDescription()
			{
			VideoDescription = $"Our Bible Hour presentation on {VideoDate.ToLongDateString()}. The speaker is {Speaker} from the {Ecclesia} Christadelphians.";
			}
		}
	}
