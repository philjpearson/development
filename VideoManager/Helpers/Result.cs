//
//	Last mod:	11 July 2016 11:26:34
//
namespace VideoManager.Helpers
	{
	public class Result
		{
		public bool Success { get; private set; }
		public string Error { get; private set; }

		protected internal Result()
			{
			Success = false;
			Error = "unknown error";
			}

		protected internal void SetSuccess()
			{
			Success = true;
			Error = string.Empty;
			}

		protected internal void SetError(string error)
			{
			Success = false;
			Error = error;
			}
		}
	}
