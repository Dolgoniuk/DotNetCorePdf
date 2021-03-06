﻿/*
MIT License

Copyright (c) 2017 WriteLinez L.L.C.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */
using DotNetCorePdf.Models;
using DotNetCorePdf.WkHtmlToPdfCore;
using DotNetCorePdf.WkHtmlToPdfCore.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetCorePdf
{
    public abstract class PdfConverterBase : IDisposable
    {
        private IntPtr GlobalSettingsPtr = IntPtr.Zero;
        private IntPtr ObjectSettingsPtr = IntPtr.Zero;
        private IntPtr ConverterPtr = IntPtr.Zero;

        private readonly object _converterRoot;

        public PdfConverterBase(object converterRoot)
        {
            _converterRoot = converterRoot;
        }

        #region Properties
        /// <summary>
        /// Called as progress is changed. (RETURNS: progressMessage)
        /// </summary>
        public Action<string> OnProgressChanged { get; set; } = null;

        /// <summary>
        /// Called when the phase is changed. (RETURNS: currentPhase, phaseCount, descriptionMessage)
        /// </summary>
        public Action<int, int, string> OnPhaseChanged { get; set; } = null;

        /// <summary>
        /// Called when there is an error in the wkhtmltopdf library. (RETURNS: errorMessage)
        /// </summary>
        public Action<string> OnError { get; set; } = null;

        /// <summary>
        /// Called when there is a warning in the wkhtmltopdf library. (RETURNS: warningMessage)
        /// </summary>
        public Action<string> OnWarning { get; set; } = null;
        #endregion

        #region Protected Methods

        protected byte[] ConvertHtmlToPdf(PdfConverterGlobalSettings globalSettings, PdfConverterObjectSettings objectSettings, byte[] data = null)
        {   
            byte[] resultBuffer = null;

            lock (_converterRoot)
            {
                try
                {
                    Dictionary<string, string> sGlobalSettings = ObjectToDictionaryConverter.Convert(globalSettings);
                    Dictionary<string, string> sObjectSettings = ObjectToDictionaryConverter.Convert(objectSettings);

                    IntPtr globalSettingsPtr = WkHtmlToPdfImports.wkhtmltopdf_create_global_settings();
                    GlobalSettingsPtr = globalSettingsPtr;
                    foreach (var globalSetting in sGlobalSettings)
                    {
                        WkHtmlToPdfImports.wkhtmltopdf_set_global_setting(globalSettingsPtr, globalSetting.Key, globalSetting.Value);
                    }

                    IntPtr objectSettingsPtr = WkHtmlToPdfImports.wkhtmltopdf_create_object_settings();
                    ObjectSettingsPtr = objectSettingsPtr;
                    foreach (var objectSetting in sObjectSettings)
                    {
                        WkHtmlToPdfImports.wkhtmltopdf_set_object_setting(objectSettingsPtr, objectSetting.Key, objectSetting.Value);
                    }

                    IntPtr converterPtr = WkHtmlToPdfImports.wkhtmltopdf_create_converter(globalSettingsPtr);
                    ConverterPtr = converterPtr;

                    //Set Callbacks
                    WkHtmlToPdfImports.wkhtmltopdf_set_progress_changed_callback(converterPtr, ProgressChangedCallback);
                    WkHtmlToPdfImports.wkhtmltopdf_set_phase_changed_callback(converterPtr, PhaseChangedCallback);
                    WkHtmlToPdfImports.wkhtmltopdf_set_error_callback(converterPtr, ErrorCallback);
                    WkHtmlToPdfImports.wkhtmltopdf_set_warning_callback(converterPtr, WarningCallback);

                    WkHtmlToPdfImports.wkhtmltopdf_add_object(converterPtr, objectSettingsPtr, data);

                    if (!WkHtmlToPdfImports.wkhtmltopdf_convert(converterPtr))
                    {
                        int errorCode = WkHtmlToPdfImports.wkhtmltopdf_http_error_code(converterPtr);
                        throw new WkHtmlToPdfException(errorCode);
                    }

                    IntPtr dataPtr = IntPtr.Zero;
                    int resultLen = WkHtmlToPdfImports.wkhtmltopdf_get_output(converterPtr, out dataPtr);
                    if (resultLen > 0)
                    {
                        resultBuffer = new byte[resultLen];
                        Marshal.Copy(dataPtr, resultBuffer, 0, resultLen);
                    }

                }
                finally
                {

                    if (GlobalSettingsPtr != IntPtr.Zero)
                    {
                        WkHtmlToPdfImports.wkhtmltopdf_destroy_global_settings(GlobalSettingsPtr);
                        GlobalSettingsPtr = IntPtr.Zero;
                    }
                    if (ObjectSettingsPtr != IntPtr.Zero)
                    {
                        WkHtmlToPdfImports.wkhtmltopdf_destroy_object_settings(ObjectSettingsPtr);
                        ObjectSettingsPtr = IntPtr.Zero;
                    }
                    if (ConverterPtr != IntPtr.Zero)
                    {
                        WkHtmlToPdfImports.wkhtmltopdf_destroy_converter(ConverterPtr);
                        ConverterPtr = IntPtr.Zero;
                    }
                }
            }

            return resultBuffer;
        }

        protected void ProgressChangedCallback(IntPtr converter)
        {
            IntPtr progressStrPtr = WkHtmlToPdfImports.wkhtmltopdf_progress_string(converter);
            string progressStr = Marshal.PtrToStringAnsi(progressStrPtr);
            //if (OnProgressChanged != null)
            //{
            //    IntPtr progressStrPtr = WkHtmlToPdfImports.wkhtmltopdf_progress_string(converter);
            //    string progressStr = Marshal.PtrToStringAnsi(progressStrPtr);
            //    OnProgressChanged(progressStr);
            //}
        }

        protected void PhaseChangedCallback(IntPtr converter)
        {
            int currentPhase = WkHtmlToPdfImports.wkhtmltopdf_current_phase(converter);
            int phaseCount = WkHtmlToPdfImports.wkhtmltopdf_phase_count(converter);
            IntPtr phaseDescriptionStrPtr = WkHtmlToPdfImports.wkhtmltopdf_phase_description(converter, currentPhase);
            string phaseDescriptionStr = Marshal.PtrToStringAnsi(phaseDescriptionStrPtr);
            //if (OnPhaseChanged != null)
            //{
            //    int currentPhase = WkHtmlToPdfImports.wkhtmltopdf_current_phase(converter);
            //    int phaseCount = WkHtmlToPdfImports.wkhtmltopdf_phase_count(converter);
            //    IntPtr phaseDescriptionStrPtr = WkHtmlToPdfImports.wkhtmltopdf_phase_description(converter, currentPhase);
            //    string phaseDescriptionStr = Marshal.PtrToStringAnsi(phaseDescriptionStrPtr);
            //    OnPhaseChanged(currentPhase, phaseCount, phaseDescriptionStr);
            //}
        }

        protected void ErrorCallback(IntPtr converter, string str)
        {
            if (OnError != null)
            {
                OnError(str);
            }
        }

        protected void WarningCallback(IntPtr converter, string str)
        {
            if (OnWarning != null)
            {
                OnWarning(str);
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PdfConverterBase()
        {
            // Finalizer calls Dispose(false)  
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

            }
            // free native resources if there are any.  
            //if (GlobalSettingsPtr != IntPtr.Zero)
            //{
            //    WkHtmlToPdfImports.wkhtmltopdf_destroy_global_settings(GlobalSettingsPtr);
            //}
            //if (ObjectSettingsPtr != IntPtr.Zero)
            //{
            //    WkHtmlToPdfImports.wkhtmltopdf_destroy_object_settings(ObjectSettingsPtr);
            //}
            //if (ConverterPtr != IntPtr.Zero)
            //{
            //    WkHtmlToPdfImports.wkhtmltopdf_destroy_converter(ConverterPtr);
            //}

            //WkHtmlToPdfImports.wkhtmltopdf_deinit();
        }
        #endregion
    }
}
