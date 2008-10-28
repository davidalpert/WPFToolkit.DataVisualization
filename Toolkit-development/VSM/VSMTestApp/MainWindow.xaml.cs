﻿using System;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Windows.Controls;
using ControlsLibrary;

namespace VSMTest
{
    public partial class VSMTestWindow
    {
        static VSMTestWindow()
        {
            // Shows how to use RegisterBehavior instead of setting VisualStateBehavior
            // in the control's style.
            // VisualStateBehavior.RegisterBehavior(new ProgressBarBehavior());
        }

        public VSMTestWindow()
        {
            this.InitializeComponent();
        }

        private void RainyButton_Click(object sender, RoutedEventArgs e)
        {
            WeatherGadget.Condition = WeatherCondition.Rainy;
            WeatherGadget.Temperature = "55";
            WeatherGadget.ConditionDescription = "Seattle-esque";
        }

        private void CloudyButton_Click(object sender, RoutedEventArgs e)
        {
            WeatherGadget.Condition = WeatherCondition.Cloudy;
            WeatherGadget.Temperature = "60";
            WeatherGadget.ConditionDescription = "Cloudy";
        }

        private void PartyCloudyButton_Click(object sender, RoutedEventArgs e)
        {
            WeatherGadget.Condition = WeatherCondition.PartlyCloudy;
            WeatherGadget.Temperature = "70";
            WeatherGadget.ConditionDescription = "Seattle-esque";
        }

        private void SunnyButton_Click(object sender, RoutedEventArgs e)
        {
            WeatherGadget.Condition = WeatherCondition.Sunny;
            WeatherGadget.Temperature = "80";
            WeatherGadget.ConditionDescription = "Sunny with blue skies";
        }
    }
}