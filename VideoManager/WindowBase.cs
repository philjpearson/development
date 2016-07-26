//
//	Last mod:	26 July 2016 19:07:17
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VideoManager
	{
	public class WindowBase : Window, INotifyPropertyChanged
		{
		public event PropertyChangedEventHandler PropertyChanged;

		public WindowBase() : base() { }

		protected void RaisePropertyChanged(string propertyName)
			{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
