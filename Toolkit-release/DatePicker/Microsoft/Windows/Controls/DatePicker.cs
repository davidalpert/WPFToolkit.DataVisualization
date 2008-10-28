﻿//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Windows.Automation.Peers;
using Microsoft.Windows.Controls.Primitives;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    /// Represents a control that allows the user to select a date.
    /// </summary>
    [TemplatePart(Name = DatePicker.ElementRoot, Type = typeof(Grid))]
    [TemplatePart(Name = DatePicker.ElementTextBox, Type = typeof(DatePickerTextBox))]
    [TemplatePart(Name = DatePicker.ElementButton, Type = typeof(Button))]
    [TemplatePart(Name = DatePicker.ElementPopup, Type = typeof(Popup))]
    [TemplateVisualState(Name = VisualStates.StateNormal, GroupName = VisualStates.GroupCommon)]
    [TemplateVisualState(Name = VisualStates.StateDisabled, GroupName = VisualStates.GroupCommon)]
    public partial class DatePicker : Control
    {
        #region Constants

        private const string ElementRoot = "PART_Root";
        private const string ElementTextBox = "PART_TextBox";
        private const string ElementButton = "PART_Button";
        private const string ElementPopup = "PART_Popup";

        #endregion Constants

        #region Data

        private Calendar _calendar;
        private string _defaultText;
        private ButtonBase _dropDownButton;
        private Popup _popUp;
        private bool _disablePopupReopen;
        private bool _shouldCoerceText;
        private string _coercedTextValue;
        private DatePickerTextBox _textBox;
        private IDictionary<DependencyProperty, bool> _isHandlerSuspended;

        #endregion Data

        #region Public Events

        public static readonly RoutedEvent SelectedDateChangedEvent = EventManager.RegisterRoutedEvent("SelectedDateChanged", RoutingStrategy.Direct, typeof(EventHandler<SelectionChangedEventArgs>), typeof(DatePicker));

        /// <summary>
        /// Occurs when the drop-down Calendar is closed.
        /// </summary>
        public event RoutedEventHandler CalendarClosed;

        /// <summary>
        /// Occurs when the drop-down Calendar is opened.
        /// </summary>
        public event RoutedEventHandler CalendarOpened;

        /// <summary>
        /// Occurs when text entered into the DatePicker cannot be parsed or the Date is not valid to be selected.
        /// </summary>
        public event EventHandler<DatePickerDateValidationErrorEventArgs> DateValidationError;

        /// <summary>
        /// Occurs when a date is selected.
        /// </summary>
        public event EventHandler<SelectionChangedEventArgs> SelectedDateChanged
        {
            add { AddHandler(SelectedDateChangedEvent, value); }
            remove { RemoveHandler(SelectedDateChangedEvent, value); }
        }

        #endregion Public Events

        /// <summary>
        /// Static constructor
        /// </summary>
        static DatePicker()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DatePicker), new FrameworkPropertyMetadata(typeof(DatePicker)));
            FocusableProperty.OverrideMetadata(typeof(DatePicker), new FrameworkPropertyMetadata(false));
            KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(DatePicker), new FrameworkPropertyMetadata(KeyboardNavigationMode.Once));
        }

        /// <summary>
        /// Initializes a new instance of the DatePicker class. 
        /// </summary>
        public DatePicker()
        {
            InitializeCalendar();
            this.IsEnabledChanged += new DependencyPropertyChangedEventHandler(OnDatePickerIsEnabledChanged);
            this._defaultText = string.Empty;

            // Binding to FirstDayOfWeek and DisplayDate wont work
            this.FirstDayOfWeek = DateTimeHelper.GetCurrentDateFormat().FirstDayOfWeek;
            this.DisplayDate = DateTime.Today;
        }

        #region Public properties

        #region BlackoutDates

        /// <summary>
        /// Gets the days that are not selectable.
        /// </summary>
        public CalendarBlackoutDatesCollection BlackoutDates
        {
            get { return _calendar.BlackoutDates; }
        }

        #endregion BlackoutDates

        #region CalendarStyle

        /// <summary>
        /// Gets or sets the style that is used when rendering the calendar.
        /// </summary>
        public Style CalendarStyle
        {
            get { return (Style)GetValue(CalendarStyleProperty); }
            set { SetValue(CalendarStyleProperty, value); }
        }

        /// <summary>
        /// Identifies the CalendarStyle dependency property.
        /// </summary>
        public static readonly DependencyProperty CalendarStyleProperty =
            DependencyProperty.Register(
            "CalendarStyle",
            typeof(Style),
            typeof(DatePicker));

        #endregion CalendarStyle

        #region DisplayDate

        /// <summary>
        /// Gets or sets the date to display.
        /// </summary>
        /// 
        public DateTime DisplayDate
        {
            get { return (DateTime)GetValue(DisplayDateProperty); }
            set { SetValue(DisplayDateProperty, value); }
        }

        /// <summary>
        /// Identifies the DisplayDate dependency property.
        /// </summary>
        public static readonly DependencyProperty DisplayDateProperty =
            DependencyProperty.Register(
            "DisplayDate",
            typeof(DateTime),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(null, CoerceDisplayDate));

        private static object CoerceDisplayDate(DependencyObject d, object value)
        {
            DatePicker dp = d as DatePicker;

            // We set _calendar.DisplayDate in order to get _calendar to compute the coerced value
            dp._calendar.DisplayDate = (DateTime)value;
            return dp._calendar.DisplayDate;
        }

        #endregion DisplayDate

        #region DisplayDateEnd

        /// <summary>
        /// Gets or sets the last date to be displayed.
        /// </summary>
        /// 
        public DateTime? DisplayDateEnd
        {
            get { return (DateTime?)GetValue(DisplayDateEndProperty); }
            set { SetValue(DisplayDateEndProperty, value); }
        }

        /// <summary>
        /// Identifies the DisplayDateEnd dependency property.
        /// </summary>
        public static readonly DependencyProperty DisplayDateEndProperty =
            DependencyProperty.Register(
            "DisplayDateEnd",
            typeof(DateTime?),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(OnDisplayDateEndChanged, CoerceDisplayDateEnd));

        /// <summary>
        /// DisplayDateEndProperty property changed handler.
        /// </summary>
        /// <param name="d">DatePicker that changed its DisplayDateEnd.</param>
        /// <param name="e">DependencyPropertyChangedEventArgs.</param>
        private static void OnDisplayDateEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DatePicker dp = d as DatePicker;
            Debug.Assert(dp != null);

            dp.CoerceValue(DisplayDateProperty);
        }

        private static object CoerceDisplayDateEnd(DependencyObject d, object value)
        {
            DatePicker dp = d as DatePicker;

            // We set _calendar.DisplayDateEnd in order to get _calendar to compute the coerced value
            dp._calendar.DisplayDateEnd = (DateTime?)value;
            return dp._calendar.DisplayDateEnd;
        }

        #endregion DisplayDateEnd

        #region DisplayDateStart

        /// <summary>
        /// Gets or sets the first date to be displayed.
        /// </summary>
        /// 
        public DateTime? DisplayDateStart
        {
            get { return (DateTime?)GetValue(DisplayDateStartProperty); }
            set { SetValue(DisplayDateStartProperty, value); }
        }

        /// <summary>
        /// Identifies the DisplayDateStart dependency property.
        /// </summary>
        public static readonly DependencyProperty DisplayDateStartProperty =
            DependencyProperty.Register(
            "DisplayDateStart",
            typeof(DateTime?),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(OnDisplayDateStartChanged, CoerceDisplayDateStart));

        /// <summary>
        /// DisplayDateStartProperty property changed handler.
        /// </summary>
        /// <param name="d">DatePicker that changed its DisplayDateStart.</param>
        /// <param name="e">DependencyPropertyChangedEventArgs.</param>
        private static void OnDisplayDateStartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DatePicker dp = d as DatePicker;
            Debug.Assert(dp != null);

            dp.CoerceValue(DisplayDateEndProperty);
            dp.CoerceValue(DisplayDateProperty);
        }

        private static object CoerceDisplayDateStart(DependencyObject d, object value)
        {
            DatePicker dp = d as DatePicker;

            // We set _calendar.DisplayDateStart in order to get _calendar to compute the coerced value
            dp._calendar.DisplayDateStart = (DateTime?)value;
            return dp._calendar.DisplayDateStart;
        }

        #endregion DisplayDateStart

        #region FirstDayOfWeek

        /// <summary>
        /// Gets or sets the day that is considered the beginning of the week.
        /// </summary>
        public DayOfWeek FirstDayOfWeek
        {
            get { return (DayOfWeek)GetValue(FirstDayOfWeekProperty); }
            set { SetValue(FirstDayOfWeekProperty, value); }
        }

        /// <summary>
        /// Identifies the FirstDayOfWeek dependency property.
        /// </summary>
        public static readonly DependencyProperty FirstDayOfWeekProperty =
            DependencyProperty.Register(
            "FirstDayOfWeek",
            typeof(DayOfWeek),
            typeof(DatePicker),
            null,
            Calendar.IsValidFirstDayOfWeek);

        #endregion FirstDayOfWeek

        #region IsDropDownOpen

        /// <summary>
        /// Gets or sets a value that indicates whether the drop-down Calendar is open or closed.
        /// </summary>
        public bool IsDropDownOpen
        {
            get { return (bool)GetValue(IsDropDownOpenProperty); }
            set { SetValue(IsDropDownOpenProperty, value); }
        }

        /// <summary>
        /// Identifies the IsDropDownOpen dependency property.
        /// </summary>
        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register(
            "IsDropDownOpen",
            typeof(bool),
            typeof(DatePicker),
            new PropertyMetadata(OnIsDropDownOpenChanged));

        /// <summary>
        /// IsDropDownOpenProperty property changed handler.
        /// </summary>
        /// <param name="d">DatePicker that changed its IsDropDownOpen.</param>
        /// <param name="e">DependencyPropertyChangedEventArgs.</param>
        private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DatePicker dp = d as DatePicker;
            Debug.Assert(dp != null);

            bool newValue = (bool)e.NewValue;
            if (dp._popUp != null && dp._popUp.IsOpen != newValue)
            {
                dp._popUp.IsOpen = newValue;
            }
        }

        #endregion IsDropDownOpen

        #region IsEnabled

        /// <summary>
        /// Called when the IsEnabled property changes.
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Property changed args</param>
        private void OnDatePickerIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateDisabledVisual();
        }

        #endregion IsEnabled

        #region IsTodayHighlighted

        /// <summary>
        /// Gets or sets a value that indicates whether the current date will be highlighted.
        /// </summary>
        public bool IsTodayHighlighted
        {
            get { return (bool)GetValue(IsTodayHighlightedProperty); }
            set { SetValue(IsTodayHighlightedProperty, value); }
        }

        /// <summary>
        /// Identifies the IsTodayHighlighted dependency property.
        /// </summary>
        public static readonly DependencyProperty IsTodayHighlightedProperty =
            DependencyProperty.Register(
            "IsTodayHighlighted",
            typeof(bool),
            typeof(DatePicker));

        #endregion IsTodayHighlighted

        #region SelectedDate

        /// <summary>
        /// Gets or sets the currently selected date.
        /// </summary>
        /// 
        public DateTime? SelectedDate
        {
            get { return (DateTime?)GetValue(SelectedDateProperty); }
            set { SetValue(SelectedDateProperty, value); }
        }

        /// <summary>
        /// Identifies the SelectedDate dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(
            "SelectedDate",
            typeof(DateTime?),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(OnSelectedDateChanged, CoerceSelectedDate));

        /// <summary>
        /// SelectedDateProperty property changed handler.
        /// </summary>
        /// <param name="d">DatePicker that changed its SelectedDate.</param>
        /// <param name="e">DependencyPropertyChangedEventArgs.</param>
        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DatePicker dp = d as DatePicker;
            Debug.Assert(dp != null);

            Collection<DateTime> addedItems = new Collection<DateTime>();
            Collection<DateTime> removedItems = new Collection<DateTime>();
            DateTime? addedDate;
            DateTime? removedDate;

            dp.CoerceValue(DisplayDateStartProperty);
            dp.CoerceValue(DisplayDateEndProperty);
            dp.CoerceValue(DisplayDateProperty);

            addedDate = (DateTime?)e.NewValue;
            removedDate = (DateTime?)e.OldValue;

            if (dp.SelectedDate.HasValue)
            {
                DateTime day = dp.SelectedDate.Value;
                dp.SetTextInternal(dp.DateTimeToString(day));

                // When DatePickerDisplayDateFlag is TRUE, the SelectedDate change is coming from the Calendar UI itself,
                // so, we shouldn't change the DisplayDate since it will automatically be changed by the Calendar
                if ((day.Month != dp.DisplayDate.Month || day.Year != dp.DisplayDate.Year) && !dp._calendar.DatePickerDisplayDateFlag)
                {
                    dp.DisplayDate = day;
                }

                dp._calendar.DatePickerDisplayDateFlag = false;
            }
            else
            {
                dp.SetWaterMarkText();
            }

            if (addedDate.HasValue)
            {
                addedItems.Add(addedDate.Value);
            }

            if (removedDate.HasValue)
            {
                removedItems.Add(removedDate.Value);
            }

            dp.OnSelectedDateChanged(new CalendarSelectionChangedEventArgs(DatePicker.SelectedDateChangedEvent, removedItems, addedItems));
        }

        private static object CoerceSelectedDate(DependencyObject d, object value)
        {
            DatePicker dp = d as DatePicker;

            // We set _calendar.SelectedDate in order to get _calendar to compute the coerced value
            dp._calendar.SelectedDate = (DateTime?)value;
            return dp._calendar.SelectedDate;
        }

        #endregion SelectedDate

        #region SelectedDateFormat

        /// <summary>
        /// Gets or sets the format that is used to display the selected date.
        /// </summary>
        public DatePickerFormat SelectedDateFormat
        {
            get { return (DatePickerFormat)GetValue(SelectedDateFormatProperty); }
            set { SetValue(SelectedDateFormatProperty, value); }
        }

        /// <summary>
        /// Identifies the SelectedDateFormat dependency property.
        /// </summary>
        public static readonly DependencyProperty SelectedDateFormatProperty =
            DependencyProperty.Register(
            "SelectedDateFormat",
            typeof(DatePickerFormat),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(OnSelectedDateFormatChanged),
            IsValidSelectedDateFormat);

        /// <summary>
        /// SelectedDateFormatProperty property changed handler.
        /// </summary>
        /// <param name="d">DatePicker that changed its SelectedDateFormat.</param>
        /// <param name="e">DependencyPropertyChangedEventArgs.</param>
        private static void OnSelectedDateFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DatePicker dp = d as DatePicker;
            Debug.Assert(dp != null);

            if (dp._textBox != null)
            {
                // Update DatePickerTextBox.Text
                if (string.IsNullOrEmpty(dp._textBox.Text))
                {
                    dp.SetWaterMarkText();
                }
                else
                {
                    DateTime? date = dp.ParseText(dp._textBox.Text);

                    if (date != null)
                    {
                        dp.SetTextInternal(dp.DateTimeToString((DateTime)date));
                    }
                }
            }
        }

        #endregion SelectedDateFormat

        #region Text

        /// <summary>
        /// Gets or sets the text that is displayed by the DatePicker.
        /// </summary>
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        /// <summary>
        /// Identifies the Text dependency property.
        /// </summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
            "Text",
            typeof(string),
            typeof(DatePicker),
            new FrameworkPropertyMetadata(string.Empty, OnTextChanged, OnCoerceText));

        /// <summary>
        /// TextProperty property changed handler.
        /// </summary>
        /// <param name="d">DatePicker that changed its Text.</param>
        /// <param name="e">DependencyPropertyChangedEventArgs.</param>
        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DatePicker dp = d as DatePicker;
            Debug.Assert(dp != null);

            if (!dp.IsHandlerSuspended(DatePicker.TextProperty))
            {
                string newValue = e.NewValue as string;

                if (newValue != null)
                {
                    if (dp._textBox != null)
                    {
                        dp._textBox.Text = newValue;
                    }
                    else
                    {
                        dp._defaultText = newValue;
                    }

                    dp.SetSelectedDate();
                }
                else
                {
                    dp.SetValueNoCallback(DatePicker.SelectedDateProperty, null);
                }
            }
        }

        private static object OnCoerceText(DependencyObject dObject, object baseValue)
        {
            DatePicker dp = (DatePicker)dObject;
            if (dp._shouldCoerceText)
            {
                dp._shouldCoerceText = false;
                return dp._coercedTextValue;
            }

            return baseValue;
        }

        /// <summary>
        /// Sets the local Text property without breaking bindings
        /// </summary>
        /// <param name="value"></param>
        private void SetTextInternal(string value)
        {
            if (BindingOperations.GetBindingExpressionBase(this, DatePicker.TextProperty) != null)
            {
                Text = value;
            }
            else
            {
                _shouldCoerceText = true;
                _coercedTextValue = value;
                CoerceValue(TextProperty);
            }
        }

        #endregion Text

        #endregion Public Properties

        #region Protected properties

        #endregion Protected Properties

        #region Internal Properties

        #endregion Internal Properties

        #region Private Properties
        #endregion Private Properties

        #region Public Methods

        /// <summary>
        /// Builds the visual tree for the DatePicker control when a new template is applied.
        /// </summary>
        public override void OnApplyTemplate()
        {
            if (_popUp != null)
            {
                _popUp.RemoveHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(PopUp_PreviewMouseLeftButtonDown));
                _popUp.Opened -= PopUp_Opened;
                _popUp.Closed -= PopUp_Closed;
                _popUp.Child = null;
            }

            if (_dropDownButton != null)
            {
                _dropDownButton.Click -= DropDownButton_Click;
                _dropDownButton.RemoveHandler(MouseLeaveEvent, new MouseEventHandler(DropDownButton_MouseLeave));
            }

            if (_textBox != null)
            {
                _textBox.RemoveHandler(TextBox.KeyDownEvent, new KeyEventHandler(TextBox_KeyDown));
                _textBox.RemoveHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(TextBox_TextChanged));
                _textBox.RemoveHandler(TextBox.LostFocusEvent, new RoutedEventHandler(TextBox_LostFocus));
            }

            base.OnApplyTemplate();

            _popUp = GetTemplateChild(ElementPopup) as Popup;

            if (_popUp != null)
            {
                _popUp.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(PopUp_PreviewMouseLeftButtonDown));
                _popUp.Opened += PopUp_Opened;
                _popUp.Closed += PopUp_Closed;
                _popUp.Child = this._calendar;

                if (this.IsDropDownOpen)
                {
                    this._popUp.IsOpen = true;
                }
            }

            _dropDownButton = GetTemplateChild(ElementButton) as Button;
            if (_dropDownButton != null)
            {
                _dropDownButton.Click += DropDownButton_Click;
                _dropDownButton.AddHandler(MouseLeaveEvent, new MouseEventHandler(DropDownButton_MouseLeave), true);

                // If the user does not provide a Content value in template, we provide a helper text that can be used in Accessibility
                // this text is not shown on the UI, just used for Accessibility purposes
                if (_dropDownButton.Content == null)
                {
                    _dropDownButton.Content = SR.Get(SRID.DatePicker_DropDownButtonName);
                }
            }

            _textBox = GetTemplateChild(ElementTextBox) as DatePickerTextBox;

            UpdateDisabledVisual();
            if (this.SelectedDate == null)
            {
                SetWaterMarkText();
            }

            if (_textBox != null)
            {
                _textBox.AddHandler(TextBox.KeyDownEvent, new KeyEventHandler(TextBox_KeyDown), true);
                _textBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(TextBox_TextChanged), true);
                _textBox.AddHandler(TextBox.LostFocusEvent, new RoutedEventHandler(TextBox_LostFocus), true);

                if (this.SelectedDate == null)
                {
                    if (!string.IsNullOrEmpty(this._defaultText))
                    {
                        _textBox.Text = this._defaultText;
                        SetSelectedDate();
                    }
                }
                else
                {
                    _textBox.Text = this.DateTimeToString((DateTime)this.SelectedDate);
                }
            }
        }

        /// <summary>
        /// Provides a text representation of the selected date.
        /// </summary>
        /// <returns>A text representation of the selected date, or an empty string if SelectedDate is a null reference.</returns>
        public override string ToString()
        {
            if (this.SelectedDate != null)
            {
                return this.SelectedDate.Value.ToString(DateTimeHelper.GetCurrentDateFormat());
            }
            else
            {
                return string.Empty;
            }
        }

        #endregion Public Methods

        #region Protected Methods

        /// <summary>
        /// Creates the automation peer for this DatePicker Control.
        /// </summary>
        /// <returns></returns>
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DatePickerAutomationPeer(this);
        }

        protected virtual void OnCalendarClosed(RoutedEventArgs e)
        {
            RoutedEventHandler handler = this.CalendarClosed;
            if (null != handler)
            {
                handler(this, e);
            }
        }

        protected virtual void OnCalendarOpened(RoutedEventArgs e)
        {
            RoutedEventHandler handler = this.CalendarOpened;
            if (null != handler)
            {
                handler(this, e);
            }
        }

        protected virtual void OnSelectedDateChanged(SelectionChangedEventArgs e)
        {
            RaiseEvent(e);
        }

        /// <summary>
        /// Raises the DateValidationError event.
        /// </summary>
        /// <param name="e">A DatePickerDateValidationErrorEventArgs that contains the event data.</param>
        protected virtual void OnDateValidationError(DatePickerDateValidationErrorEventArgs e)
        {
            EventHandler<DatePickerDateValidationErrorEventArgs> handler = this.DateValidationError;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        #endregion Protected Methods

        #region Private Methods

        private void SetValueNoCallback(DependencyProperty property, object value)
        {
            SetIsHandlerSuspended(property, true);
            try
            {
                SetValue(property, value);
            }
            finally
            {
                SetIsHandlerSuspended(property, false);
            }
        }

        private bool IsHandlerSuspended(DependencyProperty property)
        {
            return _isHandlerSuspended != null && _isHandlerSuspended.ContainsKey(property);
        }

        private void SetIsHandlerSuspended(DependencyProperty property, bool value)
        {
            if (value)
            {
                if (_isHandlerSuspended == null)
                {
                    _isHandlerSuspended = new Dictionary<DependencyProperty, bool>(2);
                }

                _isHandlerSuspended[property] = true;
            }
            else
            {
                if (_isHandlerSuspended != null)
                {
                    _isHandlerSuspended.Remove(property);
                }
            }
        }

        private void PopUp_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Popup popup = sender as Popup;
            if (popup != null && !popup.StaysOpen)
            {
                if (this._dropDownButton != null)
                {
                    if (this._dropDownButton.InputHitTest(e.GetPosition(this._dropDownButton)) != null)
                    {
                        // This popup is being closed by a mouse press on the drop down button
                        // The following mouse release will cause the closed popup to immediately reopen.
                        // Raise a flag to block reopeneing the popup
                        this._disablePopupReopen = true;
                    }
                }
            }
        }

        private void PopUp_Opened(object sender, EventArgs e)
        {
            if (!this.IsDropDownOpen)
            {
                this.IsDropDownOpen = true;
            }

            if (this._calendar != null)
            {
                this._calendar.DisplayMode = CalendarMode.Month;
                this._calendar.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }

            this.OnCalendarOpened(new RoutedEventArgs());
        }

        private void PopUp_Closed(object sender, EventArgs e)
        {
            if (this.IsDropDownOpen)
            {
                this.IsDropDownOpen = false;
            }

            this.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

            OnCalendarClosed(new RoutedEventArgs());
        }

        private void Calendar_DayButtonMouseUp(object sender, MouseButtonEventArgs e)
        {
            this.IsDropDownOpen = false;
        }

        private void Calendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            if (e.AddedDate != this.DisplayDate)
            {
                SetValue(DisplayDateProperty, (DateTime)e.AddedDate);
            }
        }

        private void Calendar_KeyDown(object sender, KeyEventArgs e)
        {
            Calendar c = sender as Calendar;
            Debug.Assert(c != null);

            if (!e.Handled && (e.Key == Key.Enter || e.Key == Key.Space || e.Key == Key.Escape) && c.DisplayMode == CalendarMode.Month)
            {
                this.IsDropDownOpen = false;
            }            
        }

        private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.Assert(e.AddedItems.Count < 2);

            if (e.AddedItems.Count > 0 && this.SelectedDate.HasValue && DateTime.Compare((DateTime)e.AddedItems[0], this.SelectedDate.Value) != 0)
            {
                this.SelectedDate = (DateTime?)e.AddedItems[0];
            }
            else
            {
                if (e.AddedItems.Count == 0)
                {
                    this.SelectedDate = null;
                    return;
                }

                if (!this.SelectedDate.HasValue)
                {
                    if (e.AddedItems.Count > 0)
                    {
                        this.SelectedDate = (DateTime?)e.AddedItems[0];
                    }
                }
            }
        }

        private string DateTimeToString(DateTime d)
        {
            DateTimeFormatInfo dtfi = DateTimeHelper.GetCurrentDateFormat();

            switch (this.SelectedDateFormat)
            {
                case DatePickerFormat.Short:
                    {
                        return string.Format(CultureInfo.CurrentCulture, d.ToString(dtfi.ShortDatePattern, dtfi));
                    }

                case DatePickerFormat.Long:
                    {
                        return string.Format(CultureInfo.CurrentCulture, d.ToString(dtfi.LongDatePattern, dtfi));
                    }
            }      

            return null;
        }

        private static DateTime DiscardDayTime(DateTime d)
        {
            int year = d.Year;
            int month = d.Month;
            DateTime newD = new DateTime(year, month, 1, 0, 0, 0);
            return newD;
        }

        private static DateTime? DiscardTime(DateTime? d)
        {
            if (d == null)
            {
                return null;
            }
            else
            {
                DateTime discarded = (DateTime)d;
                int year = discarded.Year;
                int month = discarded.Month;
                int day = discarded.Day;
                DateTime newD = new DateTime(year, month, day, 0, 0, 0);
                return newD;
            }
        }

        private void DropDownButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePopUp();
        }

        private void DropDownButton_MouseLeave(object sender, MouseEventArgs e)
        {
            this._disablePopupReopen = false;
        }

        private void TogglePopUp()
        {
            if (this.IsDropDownOpen)
            {
                this.IsDropDownOpen = false;
            }
            else
            {
                if (this._disablePopupReopen)
                {
                    this._disablePopupReopen = false;
                }
                else
                {
                    SetSelectedDate();
                    this.IsDropDownOpen = true;
                }
            }
        }

        private void InitializeCalendar()
        {
            _calendar = new Calendar();
            _calendar.DayButtonMouseUp += new MouseButtonEventHandler(Calendar_DayButtonMouseUp);
            _calendar.DisplayDateChanged += new EventHandler<CalendarDateChangedEventArgs>(Calendar_DisplayDateChanged);
            _calendar.SelectedDatesChanged += new EventHandler<SelectionChangedEventArgs>(Calendar_SelectedDatesChanged);
            _calendar.KeyDown += new KeyEventHandler(Calendar_KeyDown);
            _calendar.HorizontalAlignment = HorizontalAlignment.Left;
            _calendar.VerticalAlignment = VerticalAlignment.Top;

            _calendar.SelectionMode = CalendarSelectionMode.SingleDate;
            _calendar.SetBinding(Calendar.ForegroundProperty, GetDatePickerBinding(DatePicker.ForegroundProperty));            
            _calendar.SetBinding(Calendar.StyleProperty, GetDatePickerBinding(DatePicker.CalendarStyleProperty));
            _calendar.SetBinding(Calendar.IsTodayHighlightedProperty, GetDatePickerBinding(DatePicker.IsTodayHighlightedProperty));
            _calendar.SetBinding(Calendar.FirstDayOfWeekProperty, GetDatePickerBinding(DatePicker.FirstDayOfWeekProperty));
        }

        private BindingBase GetDatePickerBinding(DependencyProperty property)
        {
            Binding binding = new Binding(property.Name);
            binding.Source = this;
            return binding;
        }

        private static bool IsValidSelectedDateFormat(object value)
        {
            DatePickerFormat format = (DatePickerFormat)value;

            return format == DatePickerFormat.Long
                || format == DatePickerFormat.Short;
        }

        // iT SHOULD RETURN NULL IF THE STRING IS NOT VALID, RETURN THE DATETIME VALUE IF IT IS VALID

        /// <summary>
        /// Input text is parsed in the correct format and changed into a DateTime object.
        /// If the text can not be parsed TextParseError Event is thrown.
        /// </summary>
        private DateTime? ParseText(string text)
        {
            DateTime newSelectedDate;

            // TryParse is not used in order to be able to pass the exception to the TextParseError event
            try
            {
                newSelectedDate = DateTime.Parse(text, DateTimeHelper.GetCurrentDateFormat());

                if (Calendar.IsValidDateSelection(this._calendar, newSelectedDate))
                {
                    return newSelectedDate;
                }
                else
                {
                    DatePickerDateValidationErrorEventArgs dateValidationError = new DatePickerDateValidationErrorEventArgs(new ArgumentOutOfRangeException("text", SR.Get(SRID.Calendar_OnSelectedDateChanged_InvalidValue)), text);
                    OnDateValidationError(dateValidationError);

                    if (dateValidationError.ThrowException)
                    {
                        throw dateValidationError.Exception;
                    }
                }
            }
            catch (FormatException ex)
            {
                DatePickerDateValidationErrorEventArgs textParseError = new DatePickerDateValidationErrorEventArgs(ex, text);
                OnDateValidationError(textParseError);

                if (textParseError.ThrowException && textParseError.Exception != null)
                {
                    throw textParseError.Exception;
                }
            }

            return null;
        }

        private bool ProcessDatePickerKey(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.System:
                {
                    switch (e.SystemKey)
                    {
                        case Key.Down:
                        {
                            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                            {
                                TogglePopUp();
                                return true;
                            }

                            break;
                        }
                    }

                    break;
                }

                case Key.Enter:
                {
                    SetSelectedDate();
                    return true;
                }
            }

            return false;
        }

        private void SetSelectedDate()
        {
            if (this._textBox != null)
            {
                if (!string.IsNullOrEmpty(this._textBox.Text))
                {
                    string s = this._textBox.Text;

                    if (this.SelectedDate != null)
                    {
                        // If the string value of the SelectedDate and the TextBox string value are equal,
                        // we do not parse the string again
                        // if we do an extra parse, we lose data in M/d/yy format
                        // ex: SelectedDate = DateTime(1008,12,19) but when "12/19/08" is parsed it is interpreted as DateTime(2008,12,19)
                        string selectedDate = DateTimeToString(this.SelectedDate.Value);

                        if (selectedDate == s)
                        {
                            return;
                        }
                    }

                    DateTime? d = SetTextBoxValue(s);
                    if (!this.SelectedDate.Equals(d))
                    {
                        this.SelectedDate = d;
                    }
                }
                else
                {
                    if (this.SelectedDate != null)
                    {
                        this.SelectedDate = null;
                    }
                }
            }
            else
            {
                DateTime? d = SetTextBoxValue(_defaultText);
                if (!this.SelectedDate.Equals(d))
                {
                    this.SelectedDate = d;
                }
            }
        }

        private DateTime? SetTextBoxValue(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                SetValue(TextProperty, s);
                return this.SelectedDate;
            }
            else
            {
                DateTime? d = ParseText(s);

                if (d != null)
                {
                    SetValue(TextProperty, s);
                    return d;
                }
                else
                {
                    // If parse error:
                    // TextBox should have the latest valid selecteddate value:
                    if (this.SelectedDate != null)
                    {
                        string newtext = this.DateTimeToString((DateTime)this.SelectedDate);
                        SetValue(TextProperty, newtext);
                        return this.SelectedDate;
                    }
                    else
                    {
                        SetWaterMarkText();
                        return null;
                    }
                }
            }
        }

        private void SetWaterMarkText()
        {
            if (this._textBox != null)
            {
                DateTimeFormatInfo dtfi = DateTimeHelper.GetCurrentDateFormat();
                this.SetTextInternal(string.Empty);
                this._defaultText = string.Empty;

                switch (this.SelectedDateFormat)
                {
                    case DatePickerFormat.Long:
                        {
                            this._textBox.Watermark = string.Format(CultureInfo.CurrentCulture, SR.Get(SRID.DatePicker_WatermarkText), dtfi.LongDatePattern.ToString());
                            break;
                        }

                    case DatePickerFormat.Short:
                        {
                            this._textBox.Watermark = string.Format(CultureInfo.CurrentCulture, SR.Get(SRID.DatePicker_WatermarkText), dtfi.ShortDatePattern.ToString());
                            break;
                        }
                }
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SetSelectedDate();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = ProcessDatePickerKey(e) || e.Handled;            
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.SetValueNoCallback(DatePicker.TextProperty, this._textBox.Text);
        }

        private void UpdateDisabledVisual()
        {
            if (!IsEnabled)
            {
                VisualStates.GoToState(this, true, VisualStates.StateDisabled, VisualStates.StateNormal);
            }
            else
            {
                VisualStates.GoToState(this, true, VisualStates.StateNormal);
            }
        }

        #endregion Private Methods
    }
}
