using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using muxc = Microsoft.UI.Xaml.Controls;

using System.Diagnostics;
using Windows.Media.Protection.PlayReady;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Windows.Storage;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace QuickTxt
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {        
        public static TabView TabView;

        private TextDocument defaultTextDocument, textDocument;

        public MainPage()
        {
            this.InitializeComponent();

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;

            Window.Current.SetTitleBar(CustomDragRegion);            

            // Not sure if there is a better way to do this. Kinda dodgey.
            TabView = this.tabView;
        }

        /// <summary>
        /// This is invoked by the App if a user double clicks on a file in File Explorer
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task HandleFileActivated(IStorageItem item)
        {
            int newTabIndex = tabView.TabItems.Count;            

            if (newTabIndex == 1 && (string)defaultTabViewItem.Header == "*Untitled" && defaultTextDocument.IsTextBoxEmpty())
            {
                textDocument = defaultTextDocument;
            }
            else //if (newTabIndex > 1)
            {
                int selectedIndex;
                int existingIndex = isTextDocumentAlreadyOpen(item.Path);

                if (existingIndex == -1)
                {
                    var newTab = new muxc.TabViewItem();
                    newTab.IconSource = new muxc.SymbolIconSource() { Symbol = Symbol.Document };
                    newTab.Header = "Loading...";

                    // The Content of a TabViewItem is often a frame which hosts a page.
                    Frame frame = new Frame();
                    newTab.Content = frame;

                    frame.Navigate(typeof(TextDocument), newTabIndex); //TextPage

                    tabView.TabItems.Add(newTab);

                    textDocument = (TextDocument)frame.Content;
                    textDocument.SetTabViewItem(newTab);

                    selectedIndex = newTabIndex;
                }
                else
                {
                    selectedIndex = existingIndex;                    
                }

                tabView.SelectedIndex = selectedIndex;
            }
            
            await textDocument.OpenViaFileActivatedAsync((StorageFile)item); //, newTab            
        }

        /// <summary>
        /// Check if text document is already open. If so, return index, else return -1.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private int isTextDocumentAlreadyOpen(string path)
        {
            int index = -1;

            int count = tabView.TabItems.Count;

            TabViewItem item;
            Frame frame;            

            for (int i = 0; i < count; i++)
            {
                item = (TabViewItem)tabView.TabItems[i];
                frame = (Frame)item.Content;
                textDocument = (TextDocument)frame.Content;

                if (path == textDocument.GetFilePath())
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (FlowDirection == FlowDirection.LeftToRight)
            {
                CustomDragRegion.MinWidth = sender.SystemOverlayRightInset;
                ShellTitlebarInset.MinWidth = sender.SystemOverlayLeftInset;
            }
            else
            {
                CustomDragRegion.MinWidth = sender.SystemOverlayLeftInset;
                ShellTitlebarInset.MinWidth = sender.SystemOverlayRightInset;
            }

            CustomDragRegion.Height = ShellTitlebarInset.Height = sender.Height;
        }

        /// <summary>
        /// Add a new Tab to the TabView.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void TabView_AddTabButtonClick(muxc.TabView sender, object args)
        {
            var newTab = new muxc.TabViewItem();
            newTab.IconSource = new muxc.SymbolIconSource() { Symbol = Symbol.Page2 };
            newTab.Header = "*Untitled";

            // The Content of a TabViewItem is often a frame which hosts a page.
            Frame frame = new Frame();
            newTab.Content = frame;
            int newTabIndex = sender.TabItems.Count;
            frame.Navigate(typeof(TextDocument), newTabIndex); //TextPage            

            sender.TabItems.Add(newTab);

            TextDocument textDocument = (TextDocument)frame.Content;
            textDocument.SetTabViewItem(newTab);

            sender.SelectedIndex = newTabIndex;
        }

        /// <summary>
        /// Remove the requested tab from the TabView.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void TabView_TabCloseRequested(muxc.TabView sender, muxc.TabViewTabCloseRequestedEventArgs args)
        {
            TabViewItem item = args.Tab;

            bool? result = await HandleTabClose(item);

            if (result == true)
            {
                // Saved, continue exit
            }
            else if (result == false)
            {
                // Discard changes, continue exit
            }
            else //null
            {
                // Cancelled, do nothing
                return;
            }

            tabView.TabItems.Remove(item);
        }

        /// <summary>
        /// Checks for unsaved changes to a document, and prompts the user what to do if there are.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private async Task<bool?> HandleTabClose(TabViewItem item)
        {
            Frame frame = (Frame)item.Content;
            TextDocument document = (TextDocument)frame.Content;

            bool? result = await document.CheckForUnsavedChangesAsync();

            return result;
        }

        /// <summary>
        /// Closes all tabs, one at a time with prompts.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CloseAllTabs()
        {
            bool cancelled = false;

            for (int i = tabView.TabItems.Count - 1; i >= 0; i--) // Minus one as tab items are in a list where first (1st) index is 0 :D
            {
                TabViewItem item = (TabViewItem)tabView.TabItems[i];

                bool? result = await HandleTabClose(item);
                if (result == null)
                {
                    cancelled = true;
                    break;
                }

                tabView.TabItems.Remove(item);
            }

            return cancelled;
        }

        /// <summary>
        /// Sets up the initial document (blank at this stage).
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            defaultFrame.Navigate(typeof(TextDocument), 0); //TextPage

            defaultTextDocument = (TextDocument)defaultFrame.Content;

            Debug.WriteLine("Tab view actual height " + tabView.ActualHeight);

            var view = ApplicationView.GetForCurrentView();
            view.SetPreferredMinSize(new Size { Width = Constants.MIN_WIDTH, Height = Constants.MIN_HEIGHT });

        }        
    }
}
