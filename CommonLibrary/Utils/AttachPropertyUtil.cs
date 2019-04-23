﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace CommonLibrary.Utils
{
    public class AttachPropertyUtil
    {
        public static readonly DependencyProperty IsSelectedProperty;
        static AttachPropertyUtil()
        {
            IsSelectedProperty =
                DependencyProperty.RegisterAttached("IsSelected", typeof(bool), typeof(AttachPropertyUtil), new PropertyMetadata(false));
        }

        public static void SetIsSelected(DependencyObject db, bool value)
        {
            db.SetValue(IsSelectedProperty, value);
        }
        public static bool GetIsSelected(DependencyObject db)
        {
            return (bool)db.GetValue(IsSelectedProperty);
        }

    }
}