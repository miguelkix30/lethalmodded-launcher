using Microsoft.VisualBasic.FileIO;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;

namespace GameLauncher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string rootPath;
        private string versionFile;
        private string gameZip;
        private string gameFolder;
        private string gameExe;

        private LauncherStatus _status;
        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        PlayButton.Content = "Jugar";
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = "Actualización fallida - Reintentar";
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = "Descargando juego...";
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = "Descargando actualización...";
                        break;
                    default:
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            rootPath = Directory.GetCurrentDirectory();
            versionFile = Path.Combine(rootPath, "Version.txt");
            gameZip = Path.Combine(rootPath, "REPO-Miguelki.zip");
            gameFolder = Path.Combine(rootPath, "REPO-Miguelki"); // Updated to correct folder name
            gameExe = Path.Combine(rootPath, "REPO-Miguelki", "REPO.exe");
        }

        private void CheckForUpdates()
        {
            if (File.Exists(versionFile))
            {
                Version localVersion = new Version(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString();

                try
                {
                    WebClient webClient = new WebClient();
                    Version onlineVersion = new Version(webClient.DownloadString("http://tiny.cc/repomiguelkiversion"));

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        InstallGameFiles(true, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.ready;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Error al buscar actualizaciones: {ex}");
                }
            }
            else
            {
                InstallGameFiles(false, Version.zero);
            }
        }

        private void InstallGameFiles(bool _isUpdate, Version _onlineVersion)
        {
            try
            {
                WebClient webClient = new WebClient();
                if (_isUpdate)
                {
                    Status = LauncherStatus.downloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.downloadingGame;
                    _onlineVersion = new Version(webClient.DownloadString("http://tiny.cc/repomiguelkiversion"));
                }

                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressChangedCallback);
                
                // Clean up any existing game folders before downloading
                CleanupGameFolders();
                
                progressbar.Visibility = Visibility.Visible;
                webClient.DownloadFileAsync(new Uri("http://tiny.cc/repomiguelkiclient"), gameZip, _onlineVersion);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error al instalar el cliente: {ex}");
            }
        }

        // Helper method to ensure game folders are properly cleaned up
        private void CleanupGameFolders()
        {
            try
            {
                // Delete the main game folder if it exists
                if (Directory.Exists(gameFolder))
                {
                    Directory.Delete(gameFolder, true);
                }
                
                // Also check for any old REPO folder that might still exist
                string oldGameFolder = Path.Combine(rootPath, "REPO");
                if (Directory.Exists(oldGameFolder))
                {
                    Directory.Delete(oldGameFolder, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al limpiar las carpetas antiguas: {ex.Message}\nPor favor, cierre cualquier programa que pueda estar usando los archivos del juego.");
            }
        }

        private void DownloadProgressChangedCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            // Actualizar la barra de progreso durante la descarga
            Dispatcher.Invoke(() =>
            {
                double progressPercentage = (double)e.BytesReceived / e.TotalBytesToReceive * 100;
                progressbar.Value = progressPercentage;
            });
        }

        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                string onlineVersion = ((Version)e.UserState).ToString();
                
                // Clean up again to ensure we have a fresh install
                CleanupGameFolders();
                
                ZipFile.ExtractToDirectory(gameZip, rootPath, true);
                File.Delete(gameZip);

                File.WriteAllText(versionFile, onlineVersion);

                VersionText.Text = onlineVersion;

                progressbar.Value = 0;
                progressbar.Visibility = Visibility.Collapsed;

                Status = LauncherStatus.ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error al descargar archivos: {ex}");
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = Path.Combine(rootPath, "REPO-Miguelki");
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
        }
    }

    struct Version
    {
        internal static Version zero = new Version(0, 0, 0);

        private short major;
        private short minor;
        private short subMinor;

        internal Version(short _major, short _minor, short _subMinor)
        {
            major = _major;
            minor = _minor;
            subMinor = _subMinor;
        }
        internal Version(string _version)
        {
            string[] versionStrings = _version.Split('.');
            if (versionStrings.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }

            major = short.Parse(versionStrings[0]);
            minor = short.Parse(versionStrings[1]);
            subMinor = short.Parse(versionStrings[2]);
        }

        internal bool IsDifferentThan(Version _otherVersion)
        {
            if (major != _otherVersion.major)
            {
                return true;
            }
            else
            {
                if (minor != _otherVersion.minor)
                {
                    return true;
                }
                else
                {
                    if (subMinor != _otherVersion.subMinor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            return $"{major}.{minor}.{subMinor}";
        }
    }
}
