using System;
using System.Linq;
using Microsoft.VisualBasic;
using Mongoose.IDO.Protocol;
using Mongoose.Scripting;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Mongoose.GlobalScripts
{

    /**********************************************************************************************************/
    /* GenerateFilterStringSQL                                                                                */
    /*                                                                                                        */
    /* Author:       Andy Mercer                                                                              */
    /* Organization: Functional Devices, Inc.                                                                 */
    /*                                                                                                        */
    /* Date:         2024-02-13                                                                               */
    /*                                                                                                        */
    /* Purpose:      This script will build an SQL filter string that can be sent to Custom Load Methods      */
    /*               using the values which the user has entered while in FilterInPlace mode on a form. It    */
    /*               should be triggered from the StdFormFilterInPlaceExecute event. For an example, look at  */
    /*               the Resources form, which uses the Vendor script GenerateFilterStringXML, which is what  */
    /*               this script is based on. The difference between this and the vendor XML version is that  */
    /*               this script will give the exact same SQL string that you'd get from                      */
    /*               ThisForm.PrimaryIDOCollection.Filter.                                                    */
    /**********************************************************************************************************/

    public class GenerateFilterStringSQL : GlobalScript
    {

        public void Main()
        {

            try
            {

                // RETRIEVE AND VALIDATE THE PARAMETERS

                string variableName = this.GetParameterWithDefault(0, "vFilterString");

                // LOOP THROUGH ALL PROPERTIES AND GENERATE THE SQL STRING

                string SQL = this.GenerateFromCollection();

                // INSERT THE SQL STRING INTO THE FORM VARIABLE

                this.ThisForm.Variables(variableName).SetValue(SQL);

                // SET RETURN VALUE TO 0 FOR SUCCESS

                this.ReturnValue = "0";

            }
            catch (Exception ex)
            {

                // SHOW ERROR

                this.Application.ShowMessage("FDI_CreateRecord Error: " + ex.Message);
                this.ReturnValue = "1";

            }

        }

        public string GenerateFromCollection()
        {

            // SET UP VARIABLES

            IWSIDOCollection currentCollection = this.ThisForm.CurrentIDOCollection;
            List<string> filters = new List<string>();
            int counter;
            string propertyName;
            string propertyValue;
            string operatorString;
            string componentName;

            // LOOP THROUGH THE PROPERTIES ON THE PRIMARY IDO COLLECTION

            for (counter = 0; counter <= currentCollection.GetNumProperties() - 1; counter++)
            {

                // GET THE CURRENT PROPERTY NAME AND VALUE, AND ASSUME THAT THERE ARE NO WILDCARDS SO THE OPERATOR IS JUST "="

                propertyName = currentCollection.GetPropertyName(counter);
                propertyValue = currentCollection.CurrentItem[propertyName].Value.Trim();
                operatorString = "=";

                // IF THE PROPERTY HASN'T BEEN INTERACTED WITH BY THE USER OR ISN'T EVEN ON THE FORM, THEN SKIP TO NEXT PROPERTY

                if (propertyValue.Length > 0 && currentCollection.GetComponentsBoundToProperty(propertyName).Count > 0)
                {

                    // REPLACE THE SYTELINE WILDCARD CHARACTER WITH THE SQL WILDCARD CHARACTER

                    if (propertyValue.Contains(this.Application.WildCardCharacter)) {
                        propertyValue = propertyValue.Replace(this.Application.WildCardCharacter, "%");
                        operatorString = "LIKE";
                    }

                    // GRAB THE COMPONENT NAME

                    componentName = currentCollection.GetComponentsBoundToProperty(propertyName).First();

                    // CHECK TO SEE IF WE HAVE A WILDCARD DATE VALUE. IF THERE IS, THEN WE WILL HAVE TO SPLIT IT.

                    if (this.ThisForm.Components[componentName].DataType.ToUpper() == "DATE" & operatorString == "LIKE")
                    {

                        // TRY TO PARSE OUT THE DATE INTO THE MONTH, DAY, AND YEAR PARTS

                        Dictionary<string, string> datePartPair = ParseDateWithWildcard(propertyValue);

                        // IF WE HAVE ALL THREE PARTS

                        if (datePartPair.ContainsKey("DATE.YEAR") & datePartPair.ContainsKey("DATE.MONTH") & datePartPair.ContainsKey("DATE.DAY"))
                        {

                            // IF THE MONTH IS NOT A WILDCARD, THEN APPEND IT TO THE FILTER USING THE "DATEPART(mm, PROPERTYNAME)" SYNTAX

                            if (datePartPair["DATE.MONTH"] != "%")
                            {

                                filters.Add(BuildFilterString(
                                    propertyName: "DATEPART( mm, " + propertyName + ")",
                                    operatorString: operatorString,
                                    propertyValue: datePartPair["DATE.MONTH"]
                                ));

                            }

                            // IF THE DAY IS NOT A WILDCARD, THEN APPEND IT TO THE FILTER USING THE "DATEPART(dd, PROPERTYNAME)" SYNTAX

                            if (datePartPair["DATE.DAY"] != "%")
                            {

                                filters.Add(BuildFilterString(
                                    propertyName: "DATEPART( dd, " + propertyName + ")",
                                    operatorString: operatorString,
                                    propertyValue: datePartPair["DATE.DAY"]
                                ));

                            }

                            // IF THE YEAR IS NOT A WILDCARD, THEN APPEND IT TO THE FILTER USING THE "DATEPART(yyyy, PROPERTYNAME)" SYNTAX

                            if (datePartPair["DATE.YEAR"] != "%")
                            {

                                filters.Add(BuildFilterString(
                                    propertyName: "DATEPART( yyyy, " + propertyName + ")",
                                    operatorString: operatorString,
                                    propertyValue: datePartPair["DATE.YEAR"]
                                ));

                            }

                        }

                    }

                    // FOR ALL OTHER FILTERS BESIDES WILDCARD DATES

                    else {

                        // PARSE AND STANDARDIZE ANY DATES

                        if (this.ThisForm.Components[componentName].DataType.ToUpper() == "DATE") {
                            propertyValue = DateTime.Parse(propertyValue).ToString("yyyyMMdd HH:mm:ss", CultureInfo.InvariantCulture);
                        }

                        // APPEND THE FILTER

                        filters.Add(BuildFilterString(
                            propertyName: propertyName,
                            operatorString: operatorString,
                            propertyValue: propertyValue
                        ));

                    }

                }
            }

            // JOIN THE FILTERS LIST AND RETURN

            return string.Join(" AND ", filters);

        }

        private Dictionary<string, string> ParseDateWithWildcard(string filterDate)
        {

            // THIS PARSES THE DATE INTO A DICTIONARY OF PARTS. AS AN EXAMPLE, 9/1/2023 WOULD BECOME
            // {
            //     "DATE.YEAR" : "2023"
            //     "DATE.MONTH" : "9"
            //     "DATE.DAY" : "1"
            // }

            string[] dateFormatParts = Thread.CurrentThread.CurrentUICulture.DateTimeFormat.ShortDatePattern.Split(new char[] { '/', ' ', '-' });
            string[] dateValueParts = filterDate.Split(new char[] { '/', ' ', '-' });

            Dictionary<string, string> parsedDateParts = new Dictionary<string, string>();
            
            if (dateFormatParts.Length >= 3 & dateValueParts.Length >= 3)
            {
                for (int i = 0; i <= 2; i++)
                {
                    if (dateFormatParts[i].ToLower().Contains("y"))
                    {
                        parsedDateParts.Add("DATE.YEAR", dateValueParts[i]);
                    }
                    else if (dateFormatParts[i].ToLower().Contains("m"))
                    {
                        parsedDateParts.Add("DATE.MONTH", dateValueParts[i]);
                    }
                    else if (dateFormatParts[i].ToLower().Contains("d"))
                    {
                        parsedDateParts.Add("DATE.DAY", dateValueParts[i]);
                    }
                }
            }

            return parsedDateParts;

        }

        public string BuildFilterString(string propertyName, string operatorString, string propertyValue)
        {

            return " ( " + propertyName + " " + operatorString + " N'" + propertyValue + "' ) ";

        }

        public string GetParameterWithDefault(int index, string defaultValue = "")
        {

            string parameterValue = defaultValue;
            string rawParameterValue;

            // VALIDATE THE PARAMETER COUNT

            if (this.ParameterCount() > index)
            {

                rawParameterValue = this.GetParameter(0);

                // VALIDATE THAT THE VALUE ISN'T NULL OR EMPTY

                if (rawParameterValue != null && rawParameterValue.Length > 0)
                {
                    parameterValue = rawParameterValue;
                }

            }

            return parameterValue;

        }

    }
}
