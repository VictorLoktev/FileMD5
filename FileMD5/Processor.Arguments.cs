using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Trinet.Core.IO.Ntfs;

namespace FileMD5
{
	public partial class Processor
	{
		private static bool ProcessArguments( string[] args )
		{
			bool fault = false;
			bool catchExtract = false;
			bool catchDo = false;

			foreach( string s in args )
			{
				if( catchExtract )
				{
					ExtractDir = s;
					catchExtract = false;
					bool dirExists = false;
					try
					{
						dirExists = Directory.Exists( s );
						if( !dirExists )
							dirExists = Directory.Exists( Path.GetDirectoryName( s ) );
						if( !dirExists )
							dirExists = Directory.Exists( Path.GetDirectoryName( Path.GetDirectoryName( s ) ) );
						ExtractFullPath = Path.GetFullPath( ExtractDir );
					}
					catch { }
					if( !dirExists )
					{
						Console.WriteLine( "Директория из параметра -extract не существует" );
						fault = true;
					}
				}
				else
				{
					if( s.StartsWith( "-" ) || s.StartsWith( "/" ) )
					{
						switch( s.ToLower() )
						{
						case "-?":
						case "/?":
						case "-h":
						case "/h":
						case "-help":
						case "/help":
							Console.WriteLine( Resources.Help, WrongExtension );
							return true;

						case "-beep":
						case "-bell":
							BeepOption = true;
							break;

						case "-pause":
							PauseOption = true;
							break;

						case "-remove":
							if( CheckMD5Option )
							{
								Console.WriteLine( "Параметр -remove не может использоваться вместе с параметром -check" );
								fault = true;
							}
							if( SetMD5Option )
							{
								Console.WriteLine( "Параметр -remove не может использоваться вместе с параметром -md5" );
								fault = true;
							}
							RemoveOption = true;
							break;

						case "-md5":
							if( RemoveOption )
							{
								Console.WriteLine( "Параметр -md5 не может использоваться вместе с параметром -remove" );
								fault = true;
							}
							if( CheckMD5Option )
							{
								Console.WriteLine( "Параметр -md5 не может использоваться вместе с параметром -check" );
								fault = true;
							}
							SetMD5Option = true;
							break;

						case "-check":
							if( RemoveOption )
							{
								Console.WriteLine( "Параметр -check не может использоваться вместе с параметром -remove" );
								fault = true;
							}
							if( SetMD5Option )
							{
								Console.WriteLine( "Параметр -check не может использоваться вместе с параметром -md5" );
								fault = true;
							}
							CheckMD5Option = true;
							break;

						case "-ok+":
							if( OkMinusOption )
							{
								Console.WriteLine( "Параметр -ok+ не может использоваться вместе с параметром -ok-" );
								fault = true;
							}
							OkPlusOption = true;
							break;

						case "-ok-":
							if( OkPlusOption )
							{
								Console.WriteLine( "Параметр -ok- не может использоваться вместе с параметром -ok+" );
								fault = true;
							}
							OkMinusOption = true;
							break;

						case "-extract":
							ExtractOption = true;
							catchExtract = true;
							break;

						case "-rename":
						case "-ren":
							RenameOption = true;
							break;

						case "-offline":
						case "-off":
							OfflineAttributeOption = true;
							break;

						case "-r":
						case "-s":
							SubFoldersOption = true;
							break;

						default:
							Console.WriteLine( "Неизвестный параметр \"" + s + "\"" );
							fault = true;
							break;
						}
					}
					else
					{
						catchDo = true;
					}
				}
				if( catchDo )
				{
					if( s.IndexOfAny( Path.GetInvalidPathChars() ) >= 0 )
					{
						Console.WriteLine( "Недопустимый символ в параметре \"" + s + "\"" );
						fault = true;
					}
					if( Path.GetDirectoryName( s ).IndexOfAny( new char[] { '?', '*' } ) >= 0 )
					{
						Console.WriteLine( "Путь не может содержать символы маски (? или *), маска допустима только в конце" );
						fault = true;
					}

					Work.Add( s );
					catchDo = false;
				}
			}
			if( ExtractOption && catchExtract )
			{
				Console.WriteLine( "После ключа -extract должна быть указана директория" );
				fault = true;
			}
			return fault;
		}
	}
}
