using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
//using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;
using Windows.Storage.Streams;
using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;
using Windows.UI.ViewManagement;
using Windows.UI.Text;
using Windows.ApplicationModel.Activation;
using Windows.Graphics.Display;
using Windows.Devices.PointOfService;
using System.Security.AccessControl;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace QuickTxt
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TextDocument : Page
    {
        private int tabViewItemIndex;
        private TabViewItem tabViewItem;        
        
        private string newLine;
        private Encoding encoding; // Current document encoding
        private bool bom;
        private Encoding defaultEncoding; // Used if Encoding Detector cannot determine UTF encoding due to lack of BoM and/or Heureistics failing to return a match.        
        private Encoding newEncoding; // Encoding to use for new text documents.        
        private bool? extendedCodePageSupport;

        private StorageFile file;        
        private bool saved, justOpened = false;

        private double originalFontSize;
        private int zoomLevel = 100;

        private int lastIndexOf;

        // TO DO: Use De-registering of event instead?
        private bool isFontListBoxLoading;

        ApplicationDataContainer localSettings;

        private enum Theme { Light, Dark, Default }

        // CONSTRUCTOR
        public TextDocument()
        {
            this.InitializeComponent();

            file = null; // We use this for a New document.

            // TO DO: The below are set for new documents. But they are also set for existing documents and overwritten.
            // Optimise: Leave null and have a check if its a new or saved document and go from there.

            // ***New Document Encoding***
            //newEncoding = Encoding.Default;

            // ***Default/Fallback Encoding***
            //defaultEncoding = getDefaultEncoding();

            // ***Load/Apply Saved Settings***
            load_ApplySavedSettings();

            // ***New Line***
            //newLine = Environment.NewLine;
            newlineTextBlock.Text = NewlineName.GetNewLineFriendlyName(newLine);

            // ***Current Document Encoding***
            encoding = newEncoding;
            //encodingTextBlock.Text = encoding.EncodingName;

            // ***BOM*** (only UTF should have a BOM)
            // Need to read this from a saved variable.
            //bom = EncodingBOM.EncodingHasBOM(encoding);
            encodingTextBlock.Text = EncodingBOM.GetEncodingNameBOM(encoding, bom);
            //System.Text.UnicodeEncoding uni = new System.Text.UnicodeEncoding();            

            //if (encoding.CodePage == Encoding.UTF8.CodePage)
            //{
            //    bom = true;

            //    encodingTextBlock.Text = encodingTextBlock.Text + " BOM";
            //}
            // At this stage, we use a BOM if the Encoding type is set toa UTF standard. This makes the most sense on Windows.
            //if (Constants.UTF_CODE_PAGES.Contains(encoding.CodePage))
            //{
            //    //if (encoding.GetPreamble().Length > 0 || encoding.CodePage == Encoding.UTF7.CodePage) // There seems to be a bug in .NET where GetPreamble does not return a BOM for UTF7
            //    //{
            //        bom = true;

            //        encodingTextBlock.Text = encodingTextBlock.Text + " BOM";
            //    //}
            //}            
        }

        /// <summary>
        /// Returns the default Encoding to use when auto-detection fails (and Extended Code Pages are not available).
        /// Prefers ASCII for Australia, New Zealand and the US
        /// Prefers ISO/IEC 8859-1 for Western Europe
        /// Falls back to UTF-8 for everywhere else (in most cases this should have been auto-detected and the user should enable Extended Code Pages).
        /// </summary>
        private Encoding getDefaultEncoding()
        {            
            string language = Windows.System.UserProfile.GlobalizationPreferences.Languages.First();
            if (Constants.US_ASCII_LANGUAGES.Contains(language))
            {
                // This is the most compatible/popular encoding ever created. It is also more space effecient than the below as its 7-bit as opposed to 8-bit.
                // Will be fine for English speaking users who use a US-keyboard layout such as the US, Australia, & NZ, also the majority of Canada outside Quebec (English only).
                // No good for UK as the lack of a pound symbol. OK if you don't need this.
                return Encoding.ASCII;
            }
            else if (Constants.ISO_8859_1_LANGUAGES.Contains(language))
            {
                // Extends the above ASCII by adding Characters required for Western European languages.
                // Will be best for US (Spanish), UK, most of Western Europe.
                // Sadly, there are no other options other than UTF for users out side of the English / Western Europe speaking world.
                return Encoding.GetEncoding(28591);
            }
            else
            {
                //return Encoding.GetEncoding(0);
                return new UTF8Encoding(false);
            }
        }
        public string GetFilePath()
        {
            string path = "";

            if (file != null)
            {
                path = file.Path;
            }

            return path;
        }

        public bool IsTextBoxEmpty()
        {
            if (textBox.Text == "")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // NAVIGATED TO
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
                        
            if (tabViewItem == null)
            {
                tabViewItemIndex = (int)e.Parameter;
                // This will only work for the default/first tab. As when it's called for additional tab, the page is created before its added to the tab view. In this case, the item will be null.
                tabViewItem = (TabViewItem)MainPage.TabView.TabItems[tabViewItemIndex];
            }            
        }

        /// <summary>
        /// See above comment why this is needed. This is called *after* a page has been added to the item.
        /// TO DO: Call this for the default tab and get rid of the above method.
        /// </summary>
        /// <param name="item"></param>
        public void SetTabViewItem(TabViewItem item)
        {
            if (tabViewItem == null)
            {
                tabViewItem = item;
            }
        }

        private void textBox_Loaded(object sender, RoutedEventArgs e)
        {
            //MainPage.ParentTabView.SelectedIndex = tabViewItemIndex;

            textBox.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// Loads variables saved in local settings and applys them to the appropriate elements.
        /// </summary>
        private void load_ApplySavedSettings()
        {
            localSettings = ApplicationData.Current.LocalSettings;

            // Set newline
            string newlineSetting = (string)localSettings.Values["Newline"];
            if (newlineSetting == null)
            {
                newlineSetting = Environment.NewLine;
                localSettings.Values["Newline"] = newlineSetting;
            }
            newLine = newlineSetting;
            setNewlineMenuFlyout(newLine);

            // Set theme
            object themeSetting = localSettings.Values["Theme"];
            //Theme theme;
            if (themeSetting == null)
            {
                themeSetting = Theme.Default;
                localSettings.Values["Theme"] = (int)themeSetting;
            }
            else
            {
                themeSetting = (Theme)themeSetting;
            }

            updateThemeButtons((Theme)themeSetting);

            // Set status bar visibility
            object statusBarVisibilitySetting = localSettings.Values["StatusBarVisibility"];
            bool isStatusBarVisible;
            if (statusBarVisibilitySetting == null)
            {
                isStatusBarVisible = true;
                localSettings.Values["StatusBarVisibility"] = isStatusBarVisible;
            }
            else
            {
                isStatusBarVisible = (bool)statusBarVisibilitySetting;
            }

            statusBarAppBarToggleButton.IsChecked = isStatusBarVisible;
            statusBarToggleSwitch.IsOn = isStatusBarVisible;

            setStatusBarVisibility(isStatusBarVisible);

            //// Set text wrapping
            //object isWordWrapOnSetting = localSettings.Values["IsWordWrapOn"];
            //bool isWordWrapOn;
            //if (isWordWrapOnSetting == null)
            //{
            //    isWordWrapOn = false;
            //    localSettings.Values["IsWordWrapOn"] = isWordWrapOn;
            //}
            //else
            //{
            //    isWordWrapOn = (bool)isWordWrapOnSetting;
            //}

            //wordWrapAppBarToggleButton.IsChecked = isWordWrapOn;
            //wordWrapToggleSwitch.IsOn = isWordWrapOn;

            //handleWordWrapToggled(isWordWrapOn);

            // Extended Code Page Support
            extendedCodePageSupport = (bool?)localSettings.Values["ExtendedCodePageSupport"];
            if (extendedCodePageSupport == null)
            {
                extendedCodePageSupport = false;
                localSettings.Values["ExtendedCodePageSupport"] = extendedCodePageSupport;
            }
            codePagesCheckBox.IsChecked = extendedCodePageSupport;            

            // EXPLANATION: Encoding.GetEncoding(0) vs Encoding.Default
            // 0 seems to return the system default *non-UTF* encoding. That is, the real encoding on the OS
            // Encoding.Default seems to favour UTF even if Extented Code Pages are available
            // Hence, 0 will be used for the default/fallback encoding as existing text files could literally be decades old,
            // Default will be used for new text files as going forward UTF is the de-facto text file standard of today (95%+ of html files, which are plaintext, are encoded in UTF-8).
            if (extendedCodePageSupport ?? false)
            {
                if (Helpers.RegisterCodePagesEncodingProvider())
                {
                    int? savedCodePage = (int?)localSettings.Values["DefaultEncoding"];
                    if (savedCodePage == null)
                    {
                        savedCodePage = 0;
                        localSettings.Values["DefaultEncoding"] = savedCodePage;
                    }
                    defaultEncoding = Encoding.GetEncoding(savedCodePage ?? 0);

                    int? newCodePage = (int?)localSettings.Values["NewEncoding"];
                    if (newCodePage == null)
                    {
                        newCodePage = Encoding.Default.CodePage;
                        localSettings.Values["NewEncoding"] = newCodePage;
                    }
                    newEncoding = Encoding.GetEncoding(newCodePage ?? Encoding.Default.CodePage);

                    bool? newBOM = (bool?)localSettings.Values["NewBOM"];
                    if (newBOM == null)
                    {
                        newBOM = EncodingBOM.EncodingHasBOM(newEncoding);
                    }
                    bom = newBOM ?? false;
                }
                else // The above failed
                {                    
                    statusTextBlock.Text = "Error: " + Helpers.LastErrorMessage;

                    load_ApplySavedStandardEncoding();
                }
            }
            else
            {
                load_ApplySavedStandardEncoding();
            }

            codePagesCheckBox.Click += codePagesCheckBox_Click;

            // Set font
            object fontFamily = localSettings.Values["FontFamily"];
            if (fontFamily == null)
            {
                textBox.FontFamily = new FontFamily(Constants.FONT_FAMILY);
                localSettings.Values["FontFamily"] = textBox.FontFamily.Source;
            }
            else
            {
                textBox.FontFamily = new FontFamily(fontFamily.ToString());
            }

            // Set font style and weight
            ApplicationDataCompositeValue fontStyleWeightComposite = (ApplicationDataCompositeValue)localSettings.Values["FontStyleWeight"];
            FontStyleWeight styleWeight;
            if (fontStyleWeightComposite == null)
            {
                styleWeight = new FontStyleWeight("Regular", FontStyle.Normal, FontWeights.Normal);

                fontStyleWeightComposite = new ApplicationDataCompositeValue();
                fontStyleWeightComposite["name"] = styleWeight.Name;
                fontStyleWeightComposite["style"] = (int)styleWeight.Style;
                fontStyleWeightComposite["weight"] = (ushort)styleWeight.Weight.Weight;

                localSettings.Values["FontStyleWeight"] = fontStyleWeightComposite;
            }
            else
            {
                styleWeight = new FontStyleWeight((string)fontStyleWeightComposite["name"], (FontStyle)fontStyleWeightComposite["style"], new FontWeight { Weight = (ushort)fontStyleWeightComposite["weight"] });
            }            
            textBox.FontStyle = styleWeight.Style;
            textBox.FontWeight = styleWeight.Weight;

            object fontSize = localSettings.Values["FontSize"];
            if (fontSize == null)
            {
                textBox.FontSize = 16;
                localSettings.Values["FontSize"] = textBox.FontSize;
            }
            else
            {
                textBox.FontSize = (double)fontSize;
            }
            originalFontSize = textBox.FontSize;

            //int? showWordWrap = (int?)localSettings.Values["ShowWordWrapButton"];
            //if (showWordWrap == null)
            //{
            //    showWordWrap = (int)Visibility.Visible; // Could just put 0 but it's a bit magic numbery ;)
            //    localSettings.Values["ShowWordWrapButton"] = showWordWrap;
            //}
            //wordWrapAppBarToggleButton.Visibility = (Visibility)showWordWrap;

            int? showFont = (int?)localSettings.Values["ShowFontButton"];
            if (showFont == null)
            {
                showFont = (int)Visibility.Visible;
                localSettings.Values["ShowFontButton"] = showFont;
            }
            fontAppBarButton.Visibility = (Visibility)showFont;

            int? showStatusBar = (int?)localSettings.Values["ShowStatusBarButton"];
            if (showStatusBar == null)
            {
                showStatusBar = (int)Visibility.Visible;
                localSettings.Values["ShowStatusBarButton"] = showStatusBar;
            }
            statusBarAppBarToggleButton.Visibility = (Visibility)showStatusBar;

            int? showTheme = (int?)localSettings.Values["ShowThemeButton"];
            if (showTheme == null)
            {
                showTheme = (int)Visibility.Visible;
                localSettings.Values["ShowThemeButton"] = showTheme;
            }
            themeAppBarButton.Visibility = (Visibility)showTheme;            
        }

        /// <summary>
        /// Loads & applies saved standard/non-extended encoding if enabling this fails OR the user hasn't enabled it.
        /// </summary>
        private void load_ApplySavedStandardEncoding()
        {
            int? savedCodePage = (int?)localSettings.Values["DefaultEncoding"];
            if (savedCodePage == null)
            {
                savedCodePage = 0;
                localSettings.Values["DefaultEncoding"] = savedCodePage;
            }

            int? newCodePage = (int?)localSettings.Values["NewEncoding"];
            if (newCodePage == null)
            {
                newCodePage = Encoding.Default.CodePage;
                localSettings.Values["NewEncoding"] = newCodePage;
            }

            // TO DO: Optimise
            // If the user previously enabled extended code pages, and has switched back, the saved code page might not be availabe. We need to check this.
            EncodingInfo[] standardEncodings = Encoding.GetEncodings();
            bool encodingIsAvailable = false;
            bool newEncodingIsAvailable = false;
            foreach (EncodingInfo info in standardEncodings)
            {
                if (info.CodePage == savedCodePage)
                {
                    encodingIsAvailable = true;
                }
                if (info.CodePage == newCodePage)
                {
                    newEncodingIsAvailable = true;
                }
            }

            if (!encodingIsAvailable) // Use the default and save
            {
                defaultEncoding = getDefaultEncoding();
                localSettings.Values["DefaultEncoding"] = defaultEncoding.CodePage;
            }
            else // Use the saved value
            {
                defaultEncoding = Encoding.GetEncoding(savedCodePage ?? 0);
            }

            bool? newBOM = (bool?)localSettings.Values["NewBOM"];
            //if (newBOM == null)
            //{
            //    newBOM = EncodingBOM.EncodingHasBOM(newEncoding);
            //}            

            if (!newEncodingIsAvailable) // Use the default new encoding and save
            {
                newEncoding = Encoding.Default;
                localSettings.Values["NewEncoding"] = newEncoding.CodePage;

                if (newBOM == null)
                {
                    newBOM = EncodingBOM.EncodingHasBOM(newEncoding);
                }
            }
            else // Use the saved value for the new text document encoding
            {
                if (newBOM == null)
                {
                    // Let's enable a BOM by default if the user hasn't set this.
                    newBOM = true;
                }
                //newEncoding = Encoding.GetEncoding(newCodePage ?? Encoding.Default.CodePage);
                newEncoding = EncodingBOM.GetEncoding(newCodePage ?? Encoding.Default.CodePage, newBOM ?? true);
            }

            bom = newBOM ?? true;
        }

        /// <summary>
        /// - Updates Ln/Col in Status Bar
        /// - Enables/disables cut, copy and delete buttons if possible
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            string textFromStartToCursorPosition = textBox.Text.Substring(0, textBox.SelectionStart);
            Tuple<int, int> lnCol = Helpers.GetLineCol(textFromStartToCursorPosition);
            positionTextBlock.Text = "Ln " + lnCol.Item1 + ", Col " + lnCol.Item2;

            if (textBox.SelectionLength > 0)
            {
                cutButton.IsEnabled = true;
                copyButton.IsEnabled = true;
                deleteButton.IsEnabled = true;
            }
            else
            {
                cutButton.IsEnabled = false;
                copyButton.IsEnabled = false;
                deleteButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// - Modifies document title to show user the file has had changes and is not saved.
        /// - Enables undo button if possible.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (saved && !justOpened)
            {
                saved = false;

                contentTextBlock.Foreground = new SolidColorBrush(Colors.OrangeRed);

                if (file == null)
                {
                    tabViewItem.Header = "*" + contentTextBlock.Text;
                }
                else // not null
                {
                    tabViewItem.Header = "*" + file.Name;
                }
            }
            else if (justOpened)
            {
                justOpened = false;
            }
            
            undoAppBarButton.IsEnabled = textBox.CanUndo;

            handleEmptyTextBox();
        }

        /// <summary>
        /// If the Text Box is empty, the Find & Go To buttons will be disabled here. On the other side of the coin, the find button will be enabled the Go To button if Word Wrap is also off.
        /// </summary>
        private void handleEmptyTextBox()
        {
            if (textBox.Text.Length < 1)
            {
                goToAppBarButton.IsEnabled = false;
                findAppBarButton.IsEnabled = false;                
            }
            else
            {
                findAppBarButton.IsEnabled = true;

                if (wordWrapAppBarToggleButton.IsChecked != true)
                {
                    goToAppBarButton.IsEnabled = true;
                }
            }
        }

        // NEW BUTTON
        private async void newAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(textBox.Text))
            {
                ContentDialogResult result = await newContentDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    handleNewDocument();
                }
            }
        }

        /// <summary>
        /// Handles the logic behind creating a new document.
        /// </summary>
        private void handleNewDocument()
        {
            file = null;

            newLine = (string)localSettings.Values["Newline"];
            newlineTextBlock.Text = NewlineName.GetNewLineFriendlyName(newLine);
            setNewlineMenuFlyout(newLine);

            //encoding = Encoding.Default;
            encoding = newEncoding;
            encodingTextBlock.Text = encoding.EncodingName;
            bom = false;
            //if (encoding.CodePage == Encoding.UTF8.CodePage)
            //{
            //    bom = true;

            //    encodingTextBlock.Text = encodingTextBlock.Text + " w/ BOM";
            //}
            if (Constants.UTF_CODE_PAGES.Contains(encoding.CodePage))
            {
                if (encoding.GetPreamble().Length > 0)
                {
                    bom = true;

                    encodingTextBlock.Text = encodingTextBlock.Text + " BOM";
                }
            }

            saved = false;
            contentTextBlock.Foreground = new SolidColorBrush(Colors.OrangeRed);
            contentTextBlock.Text = "Untitled";
            tabViewItem.Header = "*" + "Untitled";
            statusTextBlock.Text = String.Empty;

            textBox.Text = String.Empty;

            deleteAppBarButton.IsEnabled = false;
        }

        //OPEN BUTTON
        private async void openAppBarButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add("*");

            file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                bool successful = await readTextFromFileAsync();

                if (successful)
                {
                    openFileCompletedSuccessfully();
                }
            }
            else
            {
                statusTextBlock.Text = "Open text file cancelled.";                
            }
        }

        /// <summary>
        /// Opens the file via File Explorer or the shell.
        /// </summary>
        /// <param name="activatedFile"></param>
        /// <returns></returns>
        public async Task OpenViaFileActivatedAsync(StorageFile activatedFile)
        {
            file = activatedFile;

            if (file != null)
            {
                bool successful = await readTextFromFileAsync();

                if (successful)
                {
                    openFileCompletedSuccessfully();
                }                
            }
            else
            {
                statusTextBlock.Text = "The file object was null.";
            }
        }

        /// <summary>
        /// Handles file text file read.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> readTextFromFileAsync(Encoding userEncoding = null)
        {
            commandBar.IsEnabled = false;
            textBox.IsEnabled = false;
            statusTextBlock.Text = "Loading...";
            progressRing.IsActive = true;

            // Ensure file is not null. THIS SHOULDN'T BE NEEDED but I am somehow getting this!
            // TO DO: Debug...
            if (file == null)
            {
                statusTextBlock.Text = "ERROR: file is null! It may have been deleted via another App/Background process.";

                commandBar.IsEnabled = true;
                textBox.IsEnabled = true;
                progressRing.IsActive = false;

                return false;
            }

            // Read bytes from file
            byte[] bytes = await Helpers.ReadBytesFromFileAsync(file);
            if (bytes == null)
            {
                statusTextBlock.Text = Helpers.LastErrorMessage;

                commandBar.IsEnabled = true;
                textBox.IsEnabled = true;
                progressRing.IsActive = false;

                return false;
            }

            bom = false;
            // Detect Encoding
            if (userEncoding == null) // Auto-detect encoding as per usual
            {
                encoding = TextFileEncodingDetector.DetectTextByteArrayEncoding(bytes, out bom);

                // If auto-detection fails and the user has not specified their own encoding, use the default/fallback encoding
                if (encoding == null)
                {
                    encoding = defaultEncoding;
                    //bom = EncodingBOM.EncodingHasBOM(encoding); // This may not be needed. As if auto-detection failed it didn't have a BOM, this will always return false.
                    bom = false;
                }
            }
            else
            {
                encoding = userEncoding; // Reload text file with user selected encoding
                bom = EncodingBOM.EncodingHasBOM(encoding);
            }

            //encodingTextBlock.Text = encoding.EncodingName;

            int bomLength = 0;
            if (bom) // Already Auto-detected
            {
                if (encoding.CodePage != Encoding.UTF7.CodePage)
                {
                    bomLength = encoding.GetPreamble().Length;
                }
                else // The joys of a partially supported UTF7.
                {
                    encoding = new UTF7BOMEncoding(); // This is so the preamble is correctly returned.

                    bomLength = UTF7BOMEncoding.GetUTF7PreambleLength(bytes.Take(bytes.Length > 5 ? 5 : bytes.Length).ToArray());

                    if (bomLength == 3)
                    {
                        bytes[3] = 43;  // Extreme dodgey hack as per above method - see comments.
                    }
                }                
            }
            else if (Constants.UTF_CODE_PAGES.Contains(encoding.CodePage)) // This is probably a Unicode file with no BOM
            {   
                // TO DO: Add all Unicode without BOM instead of just UTF7/8?
                bomLength = 0;

                // However, when/if the user SAVES the file, we want it to have a BOM as they've selected a UTF encoding.
                // At a later stage, this might be user selectable so we'd use GetPreamble().Length > 0 above
                // I don't want to do this anymore...
                //bom = true;                
            }
            else // The file either does not have a BOM, or the user is trying to force a non-UTF encoding w/ no BOM so we will treat the BOM bytes as valid text. (Highly unlikely but they asked for it!)
            {
                // Not sure if this is really required given most of the above case has been commented out...
                bomLength = 0;

                // Leave BOM as false
            }

            encodingTextBlock.Text = EncodingBOM.GetEncodingNameBOM(encoding, bom);

            // Detect newline
            newLine = Helpers.DetectTextByteArrayNewLine(bytes, bomLength);
            newlineTextBlock.Text = NewlineName.GetNewLineFriendlyName(newLine);
            setNewlineMenuFlyout(newLine);

            // Decode bytes to string 
            string text = encoding.GetString(bytes, bomLength, (bytes.Length - bomLength));            
            textBox.Text = text;

            commandBar.IsEnabled = true;
            textBox.IsEnabled = true;
            statusTextBlock.Text = "";
            progressRing.IsActive = false;

            return true;
        }

        /// <summary>
        /// Sets various variables on successful completion of a new file being opened.
        /// </summary>
        private void openFileCompletedSuccessfully()
        {
            saved = true;
            justOpened = true;
            contentTextBlock.Foreground = new SolidColorBrush(Colors.Green);
            contentTextBlock.Text = file.DisplayName;
            tabViewItem.Header = file.Name;
            statusTextBlock.Text = String.Empty;
            deleteAppBarButton.IsEnabled = true;

            textBox.Focus(FocusState.Programmatic);
        }

        // SAVE BUTTON
        private async void saveAppBarButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            await handleSaveOrSaveAsAsync();
        }

        /// <summary>
        /// If there is a file already (e.g. from opening or previously saving), goes ahead and calls save method.
        /// Otherwise, if there is no file, as it is a new document, prompts the Save As dialog instead - then saves.
        /// </summary>
        /// <returns></returns>
        private async Task handleSaveOrSaveAsAsync()
        {
            //SaveType supportedOrConverted = await unsupportedEncodingChangeAsync();

            if (file != null) // && supportedOrConverted == SaveType.Supported) // An existing file is open and the format is supported
            {
                await handleSaveAsync();
            }
            else // if (file == null && supportedOrConverted == SaveType.Supported) // New file and the format is supported
            {
                await handleSaveAsAsync();
            }
            //else if (file != null && supportedOrConverted == SaveType.Converted) // An existing file is open and the format is NOT supported, use a Save As just in case
            //{
            //    await handleSaveAsAsync();
            //}
            // Else not supported or converted and user cancelled
        }


        // SAVE AS BUTTON
        private async void saveAsAppBarButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            await handleSaveAsAsync();
        }

        /// <summary>
        /// Uses the file save picker to create a file object then saves the document.
        /// </summary>
        /// <returns></returns>
        private async Task handleSaveAsAsync()
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });

            file = await picker.PickSaveFileAsync();

            if (file != null)
            {
                await handleSaveAsync();

                deleteAppBarButton.IsEnabled = true;
            }
            else
            {
                statusTextBlock.Text = "Text document Save As cancelled.";
            }
        }

        /// <summary>
        /// Checks if an opened text file's encoding is supported.
        /// </summary>
        /// <returns></returns>
        //private async Task<SaveType> unsupportedEncodingChangeAsync()
        //{
        //    int[] supportedCodePages = { Encoding.ASCII.CodePage, Encoding.UTF8.CodePage, Encoding.Unicode.CodePage, Encoding.BigEndianUnicode.CodePage };

        //    if (!supportedCodePages.Contains(encoding.CodePage))
        //    {
        //        ContentDialog unsupportedEncodingFileDialog = new ContentDialog
        //        {
        //            Title = "WARNING - Unsupported Encoding",
        //            Content = "This text file was originally created using an unsupported encoding type. Please Save As a new file name as a precaution, and an attempt will be made to write in ASCII format.",
        //            PrimaryButtonText = "OK",
        //            CloseButtonText = "Cancel"
        //        };

        //        ContentDialogResult result = await unsupportedEncodingFileDialog.ShowAsync();

        //        // Change the encoding if the user clicked the primary button.                
        //        if (result == ContentDialogResult.Primary)
        //        {
        //            // Change encoding
        //            encoding = Encoding.UTF8;

        //            return SaveType.Converted;
        //        }
        //        else
        //        {
        //            // The user clicked the CLoseButton, pressed ESC, Gamepad B, or the system back button.
        //            return SaveType.Cancel;
        //        }
        //    }

        //    // The encoding is supported for writing to file, so do nothing.
        //    return SaveType.Supported;
        //}

        /// <summary>
        /// Handles a text file save.
        /// </summary>
        /// <returns></returns>
        private async Task handleSaveAsync()
        {
            commandBar.IsEnabled = false;
            textBox.IsEnabled = false;
            statusTextBlock.Text = "Saving...";
            progressRing.IsActive = true;

            string textToSave = Regex.Replace(textBox.Text, Constants.CLASSIC_MACOS_NEWLINE.ToString(), newLine);
            bool success = await Helpers.WriteTextToFileAsync(textToSave, encoding, file, bom);

            if (success)
            { 
                saved = true;
                contentTextBlock.Foreground = new SolidColorBrush(Colors.Green);
                contentTextBlock.Text = file.DisplayName;
                tabViewItem.Header = file.Name;
                statusTextBlock.Text = String.Empty;
            }
            else
            {
                statusTextBlock.Text = Helpers.LastErrorMessage;
            }

            progressRing.IsActive = false;            
            textBox.IsEnabled = true;
            commandBar.IsEnabled = true;
        }

        // UNDO BUTTON
        private void undoAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (textBox.CanUndo)
            {
                textBox.Undo();
            }
        }

        // CUT BUTTON
        private void cutButton_Click(object sender, RoutedEventArgs e)
        {
            textBox.CutSelectionToClipboard();
        }

        // COPY BUTTON
        private void copyButton_Click(object sender, RoutedEventArgs e)
        {
            textBox.CopySelectionToClipboard();
        }

        /// <summary>
        /// Checks if the user can paste content and enables the Paste button if so.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            //if (textBox.CanPasteClipboardContent)
            //{
            //    pasteButton.IsEnabled = true;
            //}
            //else
            //{
            //    pasteButton.IsEnabled = false;
            //}
            pasteButton.IsEnabled = textBox.CanPasteClipboardContent;
        }

        // PASTE BUTTON
        private void pasteButton_Click(object sender, RoutedEventArgs e)
        {
            textBox.PasteFromClipboard();
        }

        // DELETE BUTTON
        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            int startIndex = textBox.SelectionStart;

            textBox.Text = textBox.Text.Remove(startIndex, textBox.SelectionLength);

            textBox.SelectionStart = startIndex;
        }

        // GO TO BUTTON
        private async void goToAppBarButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            ContentDialogResult result = await goToContentDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                handleGoToButtonClick();
            }
            else
            {
                // User pressed Cancel, ESC, or the back arrow.                
            }
        }

        /// <summary>
        /// Allows end user to use Enter key in Go To Text Box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void goToTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                goToContentDialog.Hide();

                handleGoToButtonClick();

                textBox.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Handles Go To logic
        /// </summary>
        private void handleGoToButtonClick()
        {
            // If this is a number, try and go to the line, otherwise do nothing.
            if (Int32.TryParse(goToTextBox.Text.Trim(), out int lineNo))
            {
                int charNo = Helpers.GetCursorPosition(textBox.Text, lineNo);

                textBox.SelectionStart = charNo;
                textBox.SelectionLength = 0;
            }
        }

        // SELECT ALL BUTTON
        private void selectAllButton_Click(object sender, RoutedEventArgs e)
        {
            textBox.SelectAll();
        }

        // DATE/TIME BUTTON
        private void dateTimeButton_Click(object sender, RoutedEventArgs e)
        {
            string timeDate = DateTime.Now.ToString();

            int startIndex = textBox.SelectionStart;            
            textBox.Text = textBox.Text.Insert(startIndex, timeDate);
            textBox.SelectionStart = startIndex + timeDate.Length;
        }

        // WORD WRAP BUTTON
        private void wordWrapAppBarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (((AppBarToggleButton)sender).IsChecked) ?? false;

            //wordWrapToggleSwitch.IsOn = isChecked;

            handleWordWrapToggled(isChecked); 
        }

        /// <summary>
        /// - Sets the text box to wrap, or not. Disables the Go To button if wrapping is on, because line numbers will no longer match what appears on the screen.
        /// TO DO: Add a line number column? Might also be able to have this if wrapping is on by skipping rows with blank spaces.
        /// </summary>
        /// <param name="wrapped"></param>
        private void handleWordWrapToggled(bool wrapped)
        {
            localSettings.Values["IsWordWrapOn"] = wrapped;

            if (wrapped)
            {
                textBox.TextWrapping = TextWrapping.Wrap;
                goToAppBarButton.IsEnabled = false;
                fitToTextAppBarButton.IsEnabled = false;
            }
            else
            {
                textBox.TextWrapping = TextWrapping.NoWrap;
                goToAppBarButton.IsEnabled = true;
                fitToTextAppBarButton.IsEnabled = true;
            }
        }

        // ZOOM IN BUTTON
        private void zoomInButton_Click(object sender, RoutedEventArgs e)
        {
            zoomLevel = zoomLevel + 10;
            if (zoomLevel >= 500)
            {
                zoomInButton.IsEnabled = false;
            }
            else if (zoomLevel >= 20 && !zoomOutButton.IsEnabled)
            {
                zoomOutButton.IsEnabled = true;
            }

            textBox.FontSize = originalFontSize * zoomLevel / 100;
            zoomTextBlock.Text = zoomLevel + "%";
        }

        // ZOOM OUT BUTTON
        private void zoomOutButton_Click(object sender, RoutedEventArgs e)
        {            
            zoomLevel = zoomLevel - 10;
            if (zoomLevel <= 10)
            {
                zoomOutButton.IsEnabled = false;
            }
            else if (zoomLevel <= 490 && !zoomInButton.IsEnabled)
            {
                zoomInButton.IsEnabled = true;
            }

            textBox.FontSize = originalFontSize * zoomLevel / 100;
            zoomTextBlock.Text = zoomLevel + "%";
        }

        // DEFAULT ZOOM BUTTON
        private void defaultZoomButton_Click(object sender, RoutedEventArgs e)
        {
            zoomLevel = 100;
            textBox.FontSize = originalFontSize;
            zoomTextBlock.Text = zoomLevel + "%";

            if (!zoomInButton.IsEnabled)
            {
                zoomInButton.IsEnabled = true;
            }
            else if (!zoomOutButton.IsEnabled)
            {
                zoomOutButton.IsEnabled = true;
            }
        }

        // STATUS BAR TOGGLE BUTTON
        private void statusBarAppBarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = statusBarAppBarToggleButton.IsChecked ?? false;

            statusBarToggleSwitch.IsOn = isChecked;

            setStatusBarVisibility(isChecked);
        }

        /// <summary>
        /// Sets Status Bar visibility.
        /// </summary>
        /// <param name="isVisible"></param>
        private void setStatusBarVisibility(bool visible)
        {
            if (statusBarGrid == null)
            {
                return;
            }

            localSettings.Values["StatusBarVisibility"] = visible;

            if (visible)
            {
                statusBarGrid.Visibility = Visibility.Visible;
            }
            else
            {
                statusBarGrid.Visibility = Visibility.Collapsed;
            }
        }

        // THEME TOGGLE BOTTON
        private void themeAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            int nextTheme = (int)Enum.Parse(typeof(Theme), themeAppBarButton.Label) + 1;
            if (nextTheme > 2)
            {
                nextTheme = 0;
            }
            updateThemeButtons((Theme)nextTheme);
        }

        /// <summary>
        /// Updates the theme button icon & label to match the currently selected theme.
        /// Originally this was set to the "next" theme but it just didn't seem right.
        /// Having it set to the current theme makes it easy to identify what the button is for.
        /// </summary>
        /// <param name="theme"></param>
        private void updateThemeButtons(Theme theme)
        {
            switch (theme)
            {
                case Theme.Dark:
                    themeFontIcon.Glyph = "\uE708";
                    themeAppBarButton.Label = "Dark";

                    darkRadioButton.IsChecked = true;

                    setTheme(Theme.Dark);

                    break;
                case Theme.Default:
                    themeFontIcon.Glyph = "\uF08C";
                    themeAppBarButton.Label = "Default";

                    defaultRadioButton.IsChecked = true;


                    setTheme(Theme.Default);

                    break;
                case Theme.Light:
                    themeFontIcon.Glyph = "\uE706";
                    themeAppBarButton.Label = "Light";

                    lightRadioButton.IsChecked = true;

                    setTheme(Theme.Light);

                    break;
            }
        }

        /// <summary>
        /// Sets the Theme.
        /// </summary>
        /// <param name="dark"></param>
        private void setTheme(Theme theme)
        {
            localSettings.Values["Theme"] = (int)theme;

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;

            if (theme == Theme.Dark)
            {                
                // Title Bar
                titleBar.BackgroundColor = Colors.Black;
                titleBar.ForegroundColor = Colors.White;
                titleBar.ButtonBackgroundColor = Colors.Black;
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonHoverBackgroundColor = Colors.Gray;
                titleBar.ButtonPressedForegroundColor = Colors.White;
                titleBar.ButtonPressedBackgroundColor = Colors.Gray;

                titleBar.InactiveBackgroundColor = Colors.Black;
                titleBar.InactiveForegroundColor = Colors.Gray;
                titleBar.ButtonInactiveBackgroundColor = Colors.Black;
                titleBar.ButtonInactiveForegroundColor = Colors.Gray;

                // Everything else
                MainPage.TabView.RequestedTheme = ElementTheme.Dark;
                MainPage.TabView.Background = new SolidColorBrush((Color)titleBar.BackgroundColor);
                // TO DO: Tab View Item Header Outline/Shadow needs to be Dark Gray not pure Black. Currently it is not possible to see the outline of the tab in dark mode.
            }
            else if (theme == Theme.Light)
            {               
                //Title Bar
                titleBar.BackgroundColor = Colors.White;
                titleBar.ForegroundColor = Colors.Black;
                titleBar.ButtonBackgroundColor = Colors.White;
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonHoverBackgroundColor = Colors.Gainsboro;
                titleBar.ButtonPressedForegroundColor = Colors.Black;
                titleBar.ButtonPressedBackgroundColor = Colors.Gainsboro;

                titleBar.InactiveBackgroundColor = Colors.White;
                titleBar.InactiveForegroundColor = Colors.Gainsboro;
                titleBar.ButtonInactiveBackgroundColor = Colors.White;
                titleBar.ButtonInactiveForegroundColor = Colors.Gainsboro;

                //Everything else                
                MainPage.TabView.RequestedTheme = ElementTheme.Light;
                MainPage.TabView.Background = new SolidColorBrush((Color)titleBar.BackgroundColor);
            }
            else // Default/System Theme
            {
                // Set active window colors
                titleBar.ForegroundColor = Windows.UI.Colors.White;
                titleBar.BackgroundColor = (Color)Resources["SystemAccentColor"];
                titleBar.ButtonForegroundColor = Windows.UI.Colors.White;
                titleBar.ButtonBackgroundColor = (Color)Resources["SystemAccentColor"];
                titleBar.ButtonHoverForegroundColor = Windows.UI.Colors.White;
                titleBar.ButtonHoverBackgroundColor = (Color)Resources["SystemAccentColorLight1"];
                titleBar.ButtonPressedForegroundColor = Windows.UI.Colors.White;
                titleBar.ButtonPressedBackgroundColor = (Color)Resources["SystemAccentColorLight2"];

                // Set inactive window colors
                titleBar.InactiveForegroundColor = Windows.UI.Colors.Gray;
                titleBar.InactiveBackgroundColor = (Color)Resources["SystemAccentColor"];
                titleBar.ButtonInactiveForegroundColor = Windows.UI.Colors.Gray;
                titleBar.ButtonInactiveBackgroundColor = (Color)Resources["SystemAccentColor"];

                //Everything else                
                MainPage.TabView.RequestedTheme = ElementTheme.Default;
                MainPage.TabView.Background = new SolidColorBrush((Color)titleBar.BackgroundColor);
            }
        }

        // ABOUT BUTTON
        private async void aboutAppBarButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            string appVersion = string.Format("Version: {0}.{1}.{2}",
                Windows.ApplicationModel.Package.Current.Id.Version.Major,
                Windows.ApplicationModel.Package.Current.Id.Version.Minor,
                Windows.ApplicationModel.Package.Current.Id.Version.Build);

            versionTextBlock.Text = appVersion;

            await aboutContentDialog.ShowAsync();
        }

        // CLOSE BUTTON (Find/Replace Flyout)
        private void findReplaceCloseButton_Click(object sender, RoutedEventArgs e)
        {
            findAppBarButton.Flyout.Hide();

            textBox.Focus(FocusState.Programmatic);
        }

        // FIND NEXT BUTTON (Find/Replace Flyout)
        private void findNextButton_Click(object sender, RoutedEventArgs e)
        {
            handleFindClick();

            scrollToCursor(lastIndexOf);

            textBox.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// - Checks the text length in the find text box and sets the Find Next, Replace Next & Replace All enabled status
        /// - Handles find if Enter key is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void findTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (findTextBox.Text.Length > 0)
            {                
                findNextButton.IsEnabled = true;
                replaceNextButton.IsEnabled = true;
                replaceAllButton.IsEnabled = true;
            }
            else
            {                
                findNextButton.IsEnabled = false;
                replaceNextButton.IsEnabled = false;
                replaceAllButton.IsEnabled = false;
            }

            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                handleFindClick();

                scrollToCursor(lastIndexOf);

                textBox.Focus(FocusState.Programmatic);
            }
        }

        // REPLACE NEXT BUTTON (Find/Replace Flyout)
        private void replaceNextButton_Click(object sender, RoutedEventArgs e)
        {
            handleFindClick();

            if (textBox.SelectionLength > 0)
            {
                handleReplaceClick();

                scrollToCursor(lastIndexOf);
            }

            textBox.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// Handles replace if Enter key is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void replaceTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (textBox.SelectionLength > 0)
                {
                    handleReplaceClick();

                    scrollToCursor(lastIndexOf);
                }

                textBox.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Handle replace text logic
        /// </summary>
        private void handleReplaceClick()
        {
            // TO DO: Needs to be optimised
            string tempText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
            tempText = tempText.Insert(textBox.SelectionStart, replaceTextBox.Text);

            int selectionStart = textBox.SelectionStart;
            int selectionLength = replaceTextBox.Text.Length;

            textBox.Text = tempText;

            textBox.SelectionStart = selectionStart;
            textBox.SelectionLength = selectionLength;
        }

        // REPLACE ALL BUTTON (Find/Replace Flyout)
        private void replaceAllButton_Click(object sender, RoutedEventArgs e)
        {
            int cursorPosition = -1; // This is needed as the final result will always be -1 in a Replace All. So this variable stores the last actual find where this wasn't the case

            handleFindClick();

            while (textBox.SelectionLength > 0)
            {
                handleReplaceClick();
                if (lastIndexOf != -1)
                {
                    cursorPosition = lastIndexOf;
                }    

                handleFindClick();
            }

            scrollToCursor(cursorPosition);

            textBox.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// Handles find text logic
        /// </summary>
        private void handleFindClick()
        {
            string textToFind = findTextBox.Text;

            // We can't find a text string that is greater in length then the entire document, do nothing.
            if (textToFind.Length > textBox.Text.Length)
            {
                return;
            }

            // Sets whether or not to ignore the case of the search text
            StringComparison comparisonType;
            if (matchCaseCheckBox.IsChecked == true)
            {
                comparisonType = StringComparison.CurrentCulture;
            }
            else
            {
                comparisonType = StringComparison.CurrentCultureIgnoreCase;
            }

            int startIndex;

            if (textBox.SelectionLength == 0) // Search from cursor position
            {
                startIndex = textBox.SelectionStart;
            }
            else // Search from selection end
            {
                startIndex = textBox.SelectionStart + textToFind.Length;

                if (startIndex > textBox.Text.Length) // This means there is less characters less in the document then the size of the search text, so we have to go back to the start.
                {
                    startIndex = 0;
                }
            }

            lastIndexOf = textBox.Text.IndexOf(textToFind, startIndex, comparisonType); // Search for text, starting from the current cursor position to the end of the document
            
            if (startIndex > 0 && lastIndexOf == -1) // If not already at the start of the document, try again from here up to the original cursor position then stop.
            {
                lastIndexOf = textBox.Text.IndexOf(textToFind, 0, startIndex, comparisonType);                
            }
            
            if (lastIndexOf == -1) // If still not found, do nothing.
            {
                textBox.SelectionLength = 0; // To further imply nothing was found, de-select any selected text - as this was most likley selected to copy/paste into the find text.
                return;
            }

            textBox.Select(lastIndexOf, textToFind.Length);

            //checkIfSelectionLengthIsZero();
        }

        /// <summary>
        /// Scrolls to curson position. Used by find/replace function if required.
        /// </summary>
        /// <param name="cursorPosition"></param>
        private void scrollToCursor(int cursorPosition)
        {
            if (cursorPosition < 1 || cursorPosition > textBox.Text.Length)
            {
                return;
            }

            // WORK AROUND: Utterely rediculous work around to scroll to selected text
            double verticalOffset = textBox.GetRectFromCharacterIndex(cursorPosition, false).Y;

            var grid = (Grid)VisualTreeHelper.GetChild(textBox, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, verticalOffset, 1.0f, true);
                break;
            }
        }

        // FONT BUTTON
        private async void fontAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            await openFontDialogAsync();
        }

        /// <summary>
        /// Sets the existing font, style/weight and size, then opens the font content dialog.
        /// </summary>
        /// <returns></returns>
        private async Task openFontDialogAsync()
        {
            isFontListBoxLoading = true;

            List<string> itemsSource = new List<string>();
            itemsSource.Add("DOS");
            itemsSource.Add("Fixedsys");
            itemsSource.AddRange(Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies());
            fontListBox.ItemsSource = itemsSource;

            // Get current Font Family
            string source = textBox.FontFamily.Source;
            if (source == "Assets\\MorePerfectDOSVGA.ttf#More Perfect DOS VGA")
            {
                source = "DOS";
            }
            else if (source == "Assets\\FSEX300.ttf#Fixedsys Excelsior 3.01")
            {
                source = "Fixedsys";
            }
            fontListBox.SelectedItem = source;
            sampleTextBlock.FontFamily = textBox.FontFamily;

            // Get current Font Style/Weight
            styleWeightListBox.SelectedItem = FontStyleWeight.GetFontStyleWeight(textBox.FontStyle, textBox.FontWeight);
            sampleTextBlock.FontStyle = textBox.FontStyle;
            sampleTextBlock.FontWeight = textBox.FontWeight;

            // Get current Font Size
            sizeListBox.SelectedItem = textBox.FontSize;
            sampleTextBlock.FontSize = textBox.FontSize;

            isFontListBoxLoading = false;

            ContentDialogResult result = await fontContentDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Set TextBox Font Family and save setting
                string selected = fontListBox.SelectedValue.ToString();
                if (selected == "DOS")
                {
                    selected = "Assets\\MorePerfectDOSVGA.ttf#More Perfect DOS VGA";
                }
                else if (selected == "Fixedsys")
                {
                    selected = "Assets\\FSEX300.ttf#Fixedsys Excelsior 3.01";
                }
                textBox.FontFamily = new FontFamily(selected);
                localSettings.Values["FontFamily"] = textBox.FontFamily.Source;

                // Set TextBox Font Style/Weight and save setting
                FontStyleWeight selectedStyleWeight = ((FontStyleWeight)styleWeightListBox.SelectedItem);
                textBox.FontStyle = selectedStyleWeight.Style;
                textBox.FontWeight = selectedStyleWeight.Weight;

                ApplicationDataCompositeValue fontStyleWeightComposite = (ApplicationDataCompositeValue)localSettings.Values["FontStyleWeight"];
                
                fontStyleWeightComposite = new ApplicationDataCompositeValue();
                fontStyleWeightComposite["name"] = selectedStyleWeight.Name;
                fontStyleWeightComposite["style"] = (int)selectedStyleWeight.Style;
                fontStyleWeightComposite["weight"] = (ushort)selectedStyleWeight.Weight.Weight;

                localSettings.Values["FontStyleWeight"] = fontStyleWeightComposite;

                // Set TextBox Font Size and save setting
                textBox.FontSize = (double)sizeListBox.SelectedItem;
                localSettings.Values["FontSize"] = textBox.FontSize;
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // Do nothing, as the user has cancelled.
            }
        }


        /// <summary>
        /// Scrolls font, style/weight & size into view if not already.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void fontContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            fontListBox.ScrollIntoView(fontListBox.SelectedItem);
            styleWeightListBox.ScrollIntoView(styleWeightListBox.SelectedItem);
            sizeListBox.ScrollIntoView(sizeListBox.SelectedItem);
        }

        /// <summary>
        /// Updates the sample text when the user selects a new font.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fontListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isFontListBoxLoading && fontListBox.SelectedValue != null)
            {
                //fontListBox.FontFamily = new FontFamily(fontListBox.SelectedValue.ToString());
                string selected = fontListBox.SelectedValue.ToString();

                // Special case for non-standard fonts included in package.
                if (selected == "DOS")
                {
                    selected = "Assets\\MorePerfectDOSVGA.ttf#More Perfect DOS VGA";
                }
                else if (selected == "Fixedsys")
                {
                    selected = "Assets\\FSEX300.ttf#Fixedsys Excelsior 3.01";
                }
                sampleTextBlock.FontFamily = new FontFamily(selected);
            }
        }

        /// <summary>
        /// Updates the sample text when the user changes the font style/weight.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void styleWeightListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isFontListBoxLoading)
            {
                sampleTextBlock.FontStyle = ((FontStyleWeight)styleWeightListBox.SelectedItem).Style;
                sampleTextBlock.FontWeight = ((FontStyleWeight)styleWeightListBox.SelectedItem).Weight;
            }
        }

        /// <summary>
        /// Updates the sample text when the user changes the font size.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sizeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isFontListBoxLoading)
            {
                sampleTextBlock.FontSize = (double)sizeListBox.SelectedItem;
            }
        }

        // SETTINGS BUTTON
        private void settingsAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (!splitView.IsPaneOpen)
            {
                setShowOnToolbarCheckBox();
                
                string newNewline = (string)localSettings.Values["Newline"];
                newlineComboBox.SelectedIndex = NewlineName.GetNewlineName(newNewline).ID;

                populateCodePages();
            }

            splitView.IsPaneOpen = !splitView.IsPaneOpen;            
        }

        // FONT BUTTON
        private async void fontButton_Click(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = false;

            await openFontDialogAsync();
        }

        // STATUS BAR TOGGLE SWITCH
        private void statusBarToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            bool isOn = statusBarToggleSwitch.IsOn;
            statusBarAppBarToggleButton.IsChecked = isOn;

            if (statusBarGrid == null)
            {
                return;
            }

            // This is a work around to stop the settings pane from automatically closing when you toggle this status bar. Instead, it will "blank it", then apply the change AFTER the pane is manually closed.
            if (isOn)
            {                
                blankRectange.Visibility = Visibility.Collapsed;
            }
            else
            {
                blankRectange.Visibility = Visibility.Visible;
            }
        }

        // LIGHT THEME RADIO BUTTON
        private void lightRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (lightRadioButton.IsChecked == true)
            {
                updateThemeButtons(Theme.Light);
            }
        }

        // DARK THEME RADIO BUTTON
        private void darkRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (darkRadioButton.IsChecked == true)
            {
                updateThemeButtons(Theme.Dark);
            }
        }

        // DEFAULT THEME RADIO BUTTON
        private void defaultRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (defaultRadioButton.IsChecked == true)
            {
                updateThemeButtons(Theme.Default);
            }
        }

        /// <summary>
        /// This checkbox "checks all" individual "show on toolbar" checkboxes.
        /// TO DO: A future optimisation, would be to save the variables only when the application is closing rather then each time a setting is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void showOnToolbarCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (showOnToolbarCheckBox.IsChecked == true)
            {
                showFontCheckBox.IsChecked = showStatusBarCheckBox.IsChecked = showThemeCheckBox.IsChecked = true;
            }
            else if (showOnToolbarCheckBox.IsChecked == false)
            {
                showFontCheckBox.IsChecked = showStatusBarCheckBox.IsChecked = showThemeCheckBox.IsChecked = false;
            }
            else //null
            {
                return;
            }

            localSettings.Values["ShowFontButton"] = (int)fontAppBarButton.Visibility;
            localSettings.Values["ShowStatusBarButton"] = (int)statusBarAppBarToggleButton.Visibility;
            localSettings.Values["ShowThemeButton"] = (int)themeAppBarButton.Visibility;            
        }

        /// <summary>
        /// If any individual "Show on Toolbar" checkbox is clicked, this updates the main checkbox state accordingly.
        /// </summary>
        private void setShowOnToolbarCheckBox()
        {
            if (showFontCheckBox.IsChecked == true && showStatusBarCheckBox.IsChecked == true && showThemeCheckBox.IsChecked == true)
            {
                showOnToolbarCheckBox.IsChecked = true;
            }
            else if (showFontCheckBox.IsChecked == false && showStatusBarCheckBox.IsChecked == false && showThemeCheckBox.IsChecked == false)
            {
                showOnToolbarCheckBox.IsChecked = false;
            }
            else
            {
                showOnToolbarCheckBox.IsChecked = null;
            }
        }

        // SHOW WORD WRAP BUTTON CHECKBOX
        private void showWordWrapCheckBox_Click(object sender, RoutedEventArgs e)
        {
            setShowOnToolbarCheckBox();

            localSettings.Values["ShowWordWrapButton"] = (int)wordWrapAppBarToggleButton.Visibility;
        }

        // SHOW FONT BUTTON CHECKBOX
        private void showFontCheckBox_Click(object sender, RoutedEventArgs e)
        {
            setShowOnToolbarCheckBox();

            localSettings.Values["ShowFontButton"] = (int)fontAppBarButton.Visibility;
        }

        // SHOW STATUS BAR BUTTON CHECKBOX
        private void showStatusBarCheckBox_Click(object sender, RoutedEventArgs e)
        {
            setShowOnToolbarCheckBox();

            localSettings.Values["ShowStatusBarButton"] = (int)statusBarAppBarToggleButton.Visibility;
        }

        // SHOW THEME BUTTON CHECKBOX
        private void showThemeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            setShowOnToolbarCheckBox();

            localSettings.Values["ShowThemeButton"] = (int)themeAppBarButton.Visibility;
        }

        /// <summary>
        /// When the settings pane is closed, updates the status bar visiblity.
        /// TO DO: Hacky. Need to re-visit at some point.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void splitView_PaneClosed(SplitView sender, object args)
        {
            setStatusBarVisibility(statusBarToggleSwitch.IsOn);

            blankRectange.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Attempts to set the Window to the size of the text file (within reason).
        /// TO DO: Hacky, again. Move some logic to Helpers as its a bit cumbersum being in this page class.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fitToTextAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Fit to Text fired...");

            int currentLineNo = 1;
            int longestLineNo = 1;
            int longestLength = 0;
            string longestLine = "";

            string[] lines = textBox.Text.Split(Constants.CLASSIC_MACOS_NEWLINE);
            foreach (string line in lines)
            {
                int lineLength = line.Length;
                if (lineLength > longestLength)
                {
                    longestLength = lineLength;
                    longestLineNo = currentLineNo;
                    longestLine = line;
                }
                currentLineNo++;
            }

            Debug.WriteLine("The longest line no is: " + longestLineNo.ToString());
            Debug.WriteLine("Longest line is " + longestLine.ToString());
            Debug.WriteLine("The length of this line is " + longestLength.ToString());
                        
            int noOfLines = lines.Length;            
            Debug.WriteLine("The number of lines is: " + lines.Length);

            TextBlock dummyTextBlock = new TextBlock();
            dummyTextBlock.FontFamily = textBox.FontFamily;
            dummyTextBlock.FontSize = textBox.FontSize;
            dummyTextBlock.FontStyle = textBox.FontStyle;
            dummyTextBlock.FontWeight = textBox.FontWeight;
            dummyTextBlock.Text = longestLine;
            dummyTextBlock.Measure(new Size(0, 0));
            dummyTextBlock.Arrange(new Rect(0, 0, 0, 0));
            double width = dummyTextBlock.ActualWidth;
            double height = dummyTextBlock.ActualHeight;

            Debug.WriteLine("Longest line in pixels: " + width + ", " + height);

            double totalWidth = width + textBox.BorderThickness.Left + textBox.BorderThickness.Right + textBox.Padding.Left + textBox.Padding.Right + textBox.Margin.Left + textBox.Margin.Right + 2;
            Debug.WriteLine("The total width is: " + totalWidth);
            double totalHeight = height * noOfLines + textBox.BorderThickness.Top + textBox.BorderThickness.Bottom + textBox.Padding.Top + textBox.Padding.Bottom + (Window.Current.Bounds.Height - textBox.ActualHeight) + 2;
            Debug.WriteLine("The total height is: " + totalHeight);

            uint screenWidthInRawPixels = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().ScreenWidthInRawPixels;
            uint screenHeightInRawPixels = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().ScreenHeightInRawPixels;
            double rawPixelsPerViewPixel = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            double screenWidthInViewPixels = System.Convert.ToDouble(screenWidthInRawPixels) / rawPixelsPerViewPixel;
            double screenHeightInViewPixels = System.Convert.ToDouble(screenHeightInRawPixels) / rawPixelsPerViewPixel;

            double offsetToScreenWidthInViewPixels = 16; //15;
            double offsetToScreenHeightInViewPixels = 16; //7.5 //40
            double taskbarHeight = 40;
            
            double maxWidth = screenWidthInViewPixels - offsetToScreenWidthInViewPixels;
            double maxHeight = screenHeightInViewPixels - offsetToScreenHeightInViewPixels - taskbarHeight;
            
            if (totalWidth > maxWidth)
            {
                totalWidth = maxWidth;
            }
            else if (totalWidth < Constants.MIN_WIDTH)
            {
                totalWidth = Constants.MIN_WIDTH;
            }

            if (totalHeight > maxHeight)
            {
                totalHeight = maxHeight;
            }
            else if (totalHeight < Constants.MIN_HEIGHT)
            {
                totalHeight = Constants.MIN_HEIGHT;                
            }

            Size size = new Size(totalWidth, totalHeight);
            //Size size = new Size(720, 1080);

            var view = ApplicationView.GetForCurrentView();            
            bool result = view.TryResizeView(size);
            System.Diagnostics.Debug.WriteLine("Tried to resize view/window result: " + result);
        }

        // TO DO: This method needs to be simplified.
        private void populateCodePages(bool defaultAndNewOrCurrent = true)
        {
            // TO DO: Needs optimisation.

            List<EncodingBOM> currentOrNewEncodingBOMs = new List<EncodingBOM>();
            List<EncodingBOM> defaultEncodingBOMs = new List<EncodingBOM>();

            if (extendedCodePageSupport ?? false)
            {
                for (int i = 0; i < Constants.CODE_PAGES.Length; i++) // i < length
                {
                    currentOrNewEncodingBOMs.Add(EncodingBOM.GetEncodingBOM(Constants.CODE_PAGES[i])); // UWP does not have a built in list for all .NET Framework / Windows Desktop encodings or code pages.
                }
            }
            else
            {
                EncodingInfo[] info = Encoding.GetEncodings(); // .NET has a built in list of "non-extended" encoding, so use this to get the code page rather than hard coded list.

                for (int i = 0; i < info.Length; i++)
                {
                    currentOrNewEncodingBOMs.Add(EncodingBOM.GetEncodingBOM(info[i].CodePage));
                }
            }

            // Add additional UTF w/ | w/o BOMs            
            foreach (Encoding encoding in Constants.ADDITIONAL_UTF_ENCODINGS)
            {
                currentOrNewEncodingBOMs.Add(new EncodingBOM(encoding));
            }

            // Sort Alphabetically.
            // Note: The reason for this is because the presense of a BOM is basically a "magic marker" which will return a 100% confirmation of the text file's encoding.
            // Some will argue its possible that these bytes which make up the BOM could occur in the wild however it is such a rare case that's its not even with adding as an exception
            // Therefore, the auto-detection of the text file will never fail and thus "falling back" to a BOM marked UTF doesn't make sense.
            // On the other hand its possible auto detection could fail especially for UTF8 without BOM.

            currentOrNewEncodingBOMs = currentOrNewEncodingBOMs.OrderBy(e => e.EncodingNameBOM).ToList();
            defaultEncodingBOMs = currentOrNewEncodingBOMs.FindAll(d => d.HasBOM == false); // We do not require a UTF encoding with a BOM as a default fall back encoding.

            if (defaultAndNewOrCurrent)
            {
                // Default Encoding
                defaultEncodingComboBox.SelectionChanged -= newOrDefaultEncodingComboBox_SelectionChanged;

                defaultEncodingComboBox.ItemsSource = defaultEncodingBOMs;
                defaultEncodingComboBox.DisplayMemberPath = "EncodingNameBOM";

                EncodingBOM item = currentOrNewEncodingBOMs.Find(i => i.CodePage == defaultEncoding.CodePage);
                defaultEncodingComboBox.SelectedItem = item;

                defaultEncodingComboBox.SelectionChanged += newOrDefaultEncodingComboBox_SelectionChanged;

                // New Encoding
                newEncodingComboBox.SelectionChanged -= newOrDefaultEncodingComboBox_SelectionChanged;

                newEncodingComboBox.ItemsSource = currentOrNewEncodingBOMs;
                newEncodingComboBox.DisplayMemberPath = "EncodingNameBOM";

                EncodingBOM item2 = currentOrNewEncodingBOMs.Single(j => j.CodePage == newEncoding.CodePage && j.HasBOM == EncodingBOM.EncodingHasBOM(newEncoding));
                newEncodingComboBox.SelectedItem = item2;

                newEncodingComboBox.SelectionChanged += newOrDefaultEncodingComboBox_SelectionChanged;
            }
            else //Current
            {
                currentEncodingComboBox.ItemsSource = currentOrNewEncodingBOMs;
                currentEncodingComboBox.DisplayMemberPath = "EncodingNameBOM";

                EncodingBOM currentEncodingBOM;
                try
                {
                    currentEncodingBOM = currentOrNewEncodingBOMs.Single(i => (i.CodePage == encoding.CodePage) && (i.HasBOM == bom));
                }
                catch (Exception e) // If the BoM doesn't match, then we have a case where the encoding is not supported, let's ignore the BoM so the user can at least open this dialog.
                {
                    Debug.WriteLine("ERROR: " + e.Message);

                    currentEncodingBOM = currentOrNewEncodingBOMs.Single(i => i.CodePage == encoding.CodePage); //&& (i.HasBOM == bom));
                }
                currentEncodingComboBox.SelectedItem = currentEncodingBOM;
            }
        }

        /// <summary>
        /// Enables extended code pages if possible, saves the setting.
        /// Sets the setting to off, advises restart required.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void codePagesCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (codePagesCheckBox.IsChecked ?? false)
            {
                if (Helpers.RegisterCodePagesEncodingProvider())
                {
                    codePagesCheckBox.Content = "Enable Legacy Code Pages";                    
                    statusTextBlock.Text = "";

                    defaultEncoding = Encoding.GetEncoding(0); // As extended code pages have been enabled, this seems to return a traditional "Code Page" (non-UTF) encoding.

                    extendedCodePageSupport = true;
                    localSettings.Values["ExtendedCodePageSupport"] = true;
                    localSettings.Values["DefaultEncoding"] = defaultEncoding.CodePage;

                    populateCodePages();
                }
                else // The above failed
                {
                    statusTextBlock.Text = "ERROR: " + Helpers.LastErrorMessage;
                }
            }
            else
            {
                // There doesn't appear to be a way to "DeRegisterCodePagesEncodingProvider".
                // As such, the only work around is to exit the app and *not* apply the setting next time (via local settings).                

                codePagesCheckBox.Content = "*Enable Legacy Code Pages";
                statusTextBlock.Text = "Restarting the app is required to disable legacy Code Pages.";                

                extendedCodePageSupport = false;
                localSettings.Values["ExtendedCodePageSupport"] = false;

                // We can't set the default encoding here because its "still enabled".
                // As such there is a check when the settings load if the encoding is available or not.
            }
        }

        /// <summary>
        /// Saves any changes to the currently selected new document or default/fallback encoding.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void newOrDefaultEncodingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems == null) { return; }

            object addedItem = e.AddedItems.FirstOrDefault();

            if (addedItem != null)
            {
                EncodingBOM addedEncoding = ((EncodingBOM)addedItem);

                ComboBox comboBox = (ComboBox)sender;
                if (comboBox.Name == "defaultEncodingComboBox")
                {
                    if (addedEncoding.CodePage != defaultEncoding.CodePage)
                    {
                        defaultEncoding = addedEncoding.Encoding;

                        localSettings.Values["DefaultEncoding"] = addedEncoding.CodePage;
                        // A default BOM is not required as the default encoding is only used where auto-detection fails, which has its own BOM logic either way. E.g. A BOM will always be detected if present.
                        // We should not display Unicode encoding with a BOM as a default option as they should be detected.
                    }
                }
                else if (comboBox.Name == "newEncodingComboBox")
                {
                    if (addedEncoding.CodePage != newEncoding.CodePage || addedEncoding.HasBOM != EncodingBOM.EncodingHasBOM(newEncoding))
                    {
                        newEncoding = addedEncoding.Encoding;

                        localSettings.Values["NewEncoding"] = addedEncoding.CodePage;
                        localSettings.Values["NewBOM"] = addedEncoding.HasBOM;
                    }
                }
            }
        }

        /// <summary>
        /// Confirms the user wants to delete the current Text Document, deletes the file and creates a new blank document in its place.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void deleteAppBarButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            if (file != null)
            {   
                ContentDialog deleteFileDialog = new ContentDialog
                {
                    Title = "Delete",
                    Content = "Do you want to delete \"" + file.Name + "\"?",
                    PrimaryButtonText = "Delete",                    
                    CloseButtonText = "Cancel"
                };

                ContentDialogResult result = await deleteFileDialog.ShowAsync();

                // Change the encoding if the user clicked the primary button.                
                if (result == ContentDialogResult.Primary)
                {
                    commandBar.IsEnabled = false;
                    textBox.IsEnabled = false;
                    statusTextBlock.Text = "Deleting...";
                    progressRing.IsActive = true;

                    await file.DeleteAsync();

                    commandBar.IsEnabled = true;
                    textBox.IsEnabled = true;
                    statusTextBlock.Text = "";
                    progressRing.IsActive = false;

                    handleNewDocument();
                }
            }
        }

        private async void encodingAppBarButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            populateCodePages(false);

            ContentDialogResult result = await encodingContentDialog.ShowAsync();

            EncodingBOM userEncoding = ((EncodingBOM)currentEncodingComboBox.SelectedItem);

            if (result == ContentDialogResult.Primary)
            {
                if (userEncoding.CodePage == encoding.CodePage && userEncoding.HasBOM == bom)
                {
                    // The user selected code page is already the current code page/bom, do nothing
                    return;
                }

                if (saved)
                {
                    GC.Collect();

                    // TO DO: Re-open current file with new encoding
                    bool success = await readTextFromFileAsync(userEncoding.Encoding);

                    if (success)
                    {
                        openFileCompletedSuccessfully();
                    }
                    // Else the above method would have outputted an error message to the status bar.
                }
                else
                {
                    // Change encoding of document to be used next save.

                    encoding = userEncoding.Encoding;                    

                    // ***BOM*** (UTF only)                    
                    bom = userEncoding.HasBOM;
                    
                    encodingTextBlock.Text = userEncoding.EncodingNameBOM;
                }
            }
        }

        private void newlineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems == null) { return; }

            object addedItem = e.AddedItems.FirstOrDefault();

            if (addedItem != null)
            {
                NewlineName newlineName = (NewlineName)addedItem;

                localSettings.Values["Newline"] = newlineName.Newline;
            }
        }

        private void newlineAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            switch (newLine)
            {
                case "\r\n":
                    windowsNewlineItem.IsChecked = true;
                    unixNewlineItem.IsChecked = false;
                    macosNewlineItem.IsChecked = false;
                    break;
                case "\n":
                    windowsNewlineItem.IsChecked = false;
                    unixNewlineItem.IsChecked = true;
                    macosNewlineItem.IsChecked = false;
                    break;
                case "\r":
                    windowsNewlineItem.IsChecked = false;
                    unixNewlineItem.IsChecked = false;
                    macosNewlineItem.IsChecked = true;
                    break;
            }
        }

        private void newlineItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenuFlyoutItem item = (ToggleMenuFlyoutItem)sender;
            string name = item.Name;

            if (name == "windowsNewlineItem")
            {
                newLine = "\r\n";
                macosNewlineItem.IsChecked = false;
                unixNewlineItem.IsChecked = false;
            }
            else if (name == "unixNewlineItem")
            {
                newLine = "\n";
                windowsNewlineItem.IsChecked = false;
                macosNewlineItem.IsChecked = false;
            }
            else // Name is macOS
            {
                newLine = "\r";
                unixNewlineItem.IsChecked = false;
                windowsNewlineItem.IsChecked = false;
            }

            newlineTextBlock.Text = NewlineName.GetNewLineFriendlyName(newLine);
        }

        private void setNewlineMenuFlyout(string newline)
        {            
            switch (newLine)
            {
                case Constants.CLASSIC_MACOS_NEWLINE:
                    macosNewlineItem.IsChecked = true;
                    unixNewlineItem.IsChecked = false;
                    windowsNewlineItem.IsChecked = false;
                    break;
                case Constants.UNIX_NEWLINE:
                    unixNewlineItem.IsChecked = true;
                    windowsNewlineItem.IsChecked = false;
                    macosNewlineItem.IsChecked = false;
                    break;
                case Constants.WINDOWS_NEWLINE:
                    windowsNewlineItem.IsChecked = true;
                    macosNewlineItem.IsChecked = false;
                    unixNewlineItem.IsChecked = false;
                    break;
            }
        }

        /// <summary>
        /// Checks for unsaved changes.
        ///  - If not saved, prompt Saves/Saves As... and returns false
        ///  - If cancelled, returns null
        ///  - If saved, returns true
        /// </summary>
        /// <returns></returns>
        public async Task<bool?> CheckForUnsavedChangesAsync()
        {
            string title = contentTextBlock.Text;
            bool isUntitledAndEmpty = title == "Untitled" && textBox.Text == "";

            if (!saved && !isUntitledAndEmpty)
            {
                ContentDialog unsupportedEncodingFileDialog = new ContentDialog
                {
                    Title = "Unsaved Changes",
                    Content = "Do you want to save changes to " + title + "?",
                    PrimaryButtonText = "Save",
                    SecondaryButtonText = "Discard Changes",
                    CloseButtonText = "Cancel"
                };

                ContentDialogResult result = await unsupportedEncodingFileDialog.ShowAsync();

                // Change the encoding if the user clicked the primary button.                
                if (result == ContentDialogResult.Primary)
                {
                    await handleSaveOrSaveAsAsync();

                    return true; // Saved
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    // Do nothing and continue exiting
                    return false; // Discard changes / not saved
                }
                else // Cancel the navigating from
                {
                    return null; // Cancel / do nothing
                }
            }
            else // Saved
            {
                return true;
            }   
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            GC.KeepAlive(file);
        }
    }
}
