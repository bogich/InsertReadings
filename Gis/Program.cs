using System;
using System.Net;
using System.IO;
using System.Data.SqlClient;
using System.Configuration;
using System.Collections.Generic;
using System.Text;
using _DeviceMeteringService = Gis.Infrastructure.DeviceMeteringService;
using _HouseManagementService = Gis.Infrastructure.HouseManagementService;
using Gis.Helpers.HelperHouseManagementService;
using Gis.Helpers.HelperDeviceMeteringService;

namespace Gis
{
    class Program
    {
        private const string _orgPPAGUID = "04b83d24-6daa-47f9-bb4f-ce9330c3094d"; //Prod 
        //private const string _orgPPAGUID = "73075d22-be00-47c3-ad82-e95be27ac276"; // SIT01
        static string _FIASHouseGuid;
        static string _MeteringDeviceRootGUID;
        static string _ContractRootGUID;
        static string _Reading;
        static DateTime _DateReading;
        static string _ExportContractRootGUID;
        static void Main(string[] args)
        {
            List<string> _Result = new List<string>();

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            #region Экспорт договоров
            HelperHouseManagementService helperHouseManagementService = new HelperHouseManagementService();
            HelperDeviceMeteringService helperDeviceMeteringService = new HelperDeviceMeteringService();

            string typeRes = null;
            do
            {
                foreach (var tmpSupplyResourceContract in helperHouseManagementService.GetSupplyResourceContractData(_orgPPAGUID, _ExportContractRootGUID).exportSupplyResourceContractResult.Items)
                {
                    typeRes = tmpSupplyResourceContract.ToString();
                    if(tmpSupplyResourceContract.GetType() == typeof(_HouseManagementService.exportSupplyResourceContractResultType))
                    {
                        var tmpExportSupplyResourceContract = tmpSupplyResourceContract as _HouseManagementService.exportSupplyResourceContractResultType;
                        if (tmpExportSupplyResourceContract.VersionStatus == _HouseManagementService.exportSupplyResourceContractResultTypeVersionStatus.Posted && tmpExportSupplyResourceContract.Item1.GetType() == typeof(_HouseManagementService.ExportSupplyResourceContractTypeLivingHouseOwner))
                        {
                            Console.WriteLine("ContractRootGUID: {0}", tmpExportSupplyResourceContract.ContractRootGUID);
                            _ContractRootGUID = tmpExportSupplyResourceContract.ContractRootGUID;
                            #region Экспорт ОЖФ
                            foreach (var tmp in helperHouseManagementService.GetSupplyResourceContractObjectAddressData(_orgPPAGUID, _ContractRootGUID).exportSupplyResourceContractObjectAddressResult.Items)
                            {
                                if (tmp.GetType() == typeof(bool))
                                    break;
                                var tmpObjectAddressData = tmp as _HouseManagementService.exportSupplyResourceContractObjectAddressResultType;
                                Console.WriteLine("FIASHouseGuid: {0}", tmpObjectAddressData.FIASHouseGuid);
                                _FIASHouseGuid = tmpObjectAddressData.FIASHouseGuid;
                                #region Экспорт ПУ
                                foreach (var tmpMeteringDeviceData in helperHouseManagementService.GetMeteringDeviceData(_orgPPAGUID, _FIASHouseGuid).exportMeteringDeviceDataResult.Items)
                                {
                                    if (!(tmpMeteringDeviceData is _HouseManagementService.exportMeteringDeviceDataResultType tmpExportMeteringDeviceData))
                                    {
                                        Console.ForegroundColor = ConsoleColor.DarkRed;
                                        Console.WriteLine("DeviceMetering is absent");
                                        Console.ResetColor();
                                        break;
                                    }
                                    else
                                    {
                                        Console.WriteLine("MeteringDeviceRootGUID: {0}", tmpExportMeteringDeviceData.MeteringDeviceRootGUID);
                                        _MeteringDeviceRootGUID = tmpExportMeteringDeviceData.MeteringDeviceRootGUID;
                                        #region Экспорт лицевых счетов
                                        foreach (var tmpAccountData in helperHouseManagementService.GetAccountData(_orgPPAGUID, _FIASHouseGuid).exportAccountResult.Items)
                                        {
                                            /*
                                             * Проверка прикручена из-за кривости схемы выцепления значений приборов через получение ЛС.
                                             * В ГИСе один ОЖФ может быть в договоре с УК и с частником. Т.к. ЛС ищутся по адресу,
                                             * в результате поиска такоко ОЖФ (присутствующего в обоих договорах) намвывалится два item.
                                             * Но по item'у, принадлежащему договору с УК, ЛС не найдется.
                                            */
                                            if (!(tmpAccountData is _HouseManagementService.exportAccountResultType tmpExportAccountData))
                                            {
                                                Console.ForegroundColor = ConsoleColor.DarkRed;
                                                Console.WriteLine("AccountData is absent");
                                                Console.ResetColor();
                                                break;
                                            }
                                            else
                                            {
                                                Console.WriteLine("AccountGUID: {0}", tmpExportAccountData.AccountGUID);
                                                Console.WriteLine("AccountNumber: {0}", tmpExportAccountData.AccountNumber);
                                                if (int.TryParse(tmpExportAccountData.AccountNumber, out int AccNumber))
                                                {
                                                    #region Получение значения показания прибора из "Частников"
                                                    string connectionString = @"Data Source=storage;Initial Catalog=master;User Id=" + ConfigurationManager.AppSettings["db_login"] + ";Password=" + ConfigurationManager.AppSettings["db_pass"];
                                                    string sqlExpression = @"SELECT CAST(t1.pvod AS INT) AS pvod
                                                               ,CAST(t2.datap AS DATE) AS datap
                                                        FROM [sql-server].[vdk].[dbo].[CHpvod] t1
                                                        INNER JOIN (SELECT plat, MAX(datap) AS datap
                                                                    FROM [sql-server].[vdk].[dbo].[CHpvod]
                                                                    GROUP BY plat) AS t2 ON t1.datap = t2.datap AND t1.plat = t2.plat
                                                        WHERE t1.vidu = 1 AND t2.datap >= @Date AND t1.plat = @Plat";
                                                    using (SqlConnection connection = new SqlConnection(connectionString))
                                                    {
                                                        connection.Open();
                                                        SqlCommand command = new SqlCommand(sqlExpression, connection);
                                                        SqlParameter _Plat = new SqlParameter("@Plat", tmpExportAccountData.AccountNumber);
                                                        command.Parameters.Add(_Plat);
                                                        SqlParameter _Date = new SqlParameter("@Date", DateTime.Now.AddMonths(-1));
                                                        command.Parameters.Add(_Date);
                                                        SqlDataReader reader = command.ExecuteReader();
                                                        while (reader.Read())
                                                        {
                                                            _Reading = Convert.ToString(reader["pvod"]);
                                                            _DateReading = Convert.ToDateTime(reader["datap"]);
                                                        }
                                                        if (reader.HasRows)
                                                        {
                                                            #region Проверка на наличие показания с заданной датой
                                                            foreach (var tmpDeviceMeteringService in helperDeviceMeteringService.GetMeteringDeviceHistory(_orgPPAGUID, _FIASHouseGuid, _MeteringDeviceRootGUID).exportMeteringDeviceHistoryResult.Items)
                                                            {
                                                                if(tmpDeviceMeteringService.GetType() == typeof(_DeviceMeteringService.ErrorMessageType))
                                                                {
                                                                    Console.ForegroundColor = ConsoleColor.Red;
                                                                    Console.WriteLine("{0}: {1}", ((_DeviceMeteringService.ErrorMessageType)tmpDeviceMeteringService).ErrorCode, ((_DeviceMeteringService.ErrorMessageType)tmpDeviceMeteringService).Description);
                                                                    Console.ResetColor();
                                                                }
                                                                else
                                                                {
                                                                    var tmpExportDeviceMeteringService = tmpDeviceMeteringService as _DeviceMeteringService.exportMeteringDeviceHistoryResultType;
                                                                    var tmpExportMeteringDeviceHistoryResultTypeOneRateDeviceValue = tmpExportDeviceMeteringService.Item as _DeviceMeteringService.exportMeteringDeviceHistoryResultTypeOneRateDeviceValue;
                                                                    var tmpExportMeteringDeviceHistoryResultTypeOneRateDeviceValueValues = tmpExportMeteringDeviceHistoryResultTypeOneRateDeviceValue.Values as _DeviceMeteringService.exportMeteringDeviceHistoryResultTypeOneRateDeviceValueValues;

                                                                    List<DateTime> _DateCurrentReading = new List<DateTime>();
                                                                    if (tmpExportMeteringDeviceHistoryResultTypeOneRateDeviceValueValues.CurrentValue != null)
                                                                    {
                                                                        foreach (var tmpCurrentValue in tmpExportMeteringDeviceHistoryResultTypeOneRateDeviceValueValues.CurrentValue)
                                                                        {
                                                                            _DateCurrentReading.Add(tmpCurrentValue.DateValue);
                                                                        }
                                                                    }

                                                                    if (_DateCurrentReading.Contains(_DateReading))
                                                                    {
                                                                        Console.ForegroundColor = ConsoleColor.DarkRed;
                                                                        Console.WriteLine("CurrentReading of current month is already exist");
                                                                        Console.ResetColor();
                                                                        _Result.Add(tmpExportAccountData.AccountNumber + ",Текущее показание " + _Reading + " с датой " + _DateReading + " уже внесено");
                                                                    }
                                                                    else
                                                                    {
                                                                        #region Импорт перечня показаний приборов учета
                                                                        foreach (var _tmpDeviceMeteringService in helperDeviceMeteringService.SetMeteringDeviceCurrentlValue(_orgPPAGUID, _FIASHouseGuid, _MeteringDeviceRootGUID, _DateReading, _Reading).ImportResult.Items)
                                                                        {
                                                                            if (_tmpDeviceMeteringService.GetType() == typeof(_DeviceMeteringService.ErrorMessageType))
                                                                            {
                                                                                Console.ForegroundColor = ConsoleColor.Red;
                                                                                Console.WriteLine("{0}: {1}", ((_DeviceMeteringService.ErrorMessageType)_tmpDeviceMeteringService).ErrorCode, ((_DeviceMeteringService.ErrorMessageType)_tmpDeviceMeteringService).Description);
                                                                                Console.ResetColor();
                                                                                _Result.Add(tmpExportAccountData.AccountNumber + "," + ((_DeviceMeteringService.ErrorMessageType)_tmpDeviceMeteringService).ErrorCode + "" + ((_DeviceMeteringService.ErrorMessageType)_tmpDeviceMeteringService).Description);
                                                                            }
                                                                            else
                                                                            {
                                                                                var tmpImportDeviceMeteringService = _tmpDeviceMeteringService as _DeviceMeteringService.CommonResultType;
                                                                                foreach (var tmpCommonResultType in tmpImportDeviceMeteringService.Items)
                                                                                {
                                                                                    if (tmpCommonResultType.GetType() == typeof(_DeviceMeteringService.CommonResultTypeError))
                                                                                    {
                                                                                        Console.ForegroundColor = ConsoleColor.Red;
                                                                                        Console.WriteLine("{0}: {1}", ((_DeviceMeteringService.CommonResultTypeError)tmpCommonResultType).ErrorCode, ((_DeviceMeteringService.CommonResultTypeError)tmpCommonResultType).Description);
                                                                                        Console.ResetColor();
                                                                                        _Result.Add(tmpExportAccountData.AccountNumber + "," + ((_DeviceMeteringService.CommonResultTypeError)tmpCommonResultType).ErrorCode + " " + ((_DeviceMeteringService.CommonResultTypeError)tmpCommonResultType).Description);

                                                                                        if (((_DeviceMeteringService.CommonResultTypeError)tmpCommonResultType).ErrorCode == "SRV007016" || ((_DeviceMeteringService.CommonResultTypeError)tmpCommonResultType).ErrorCode == "SRV007103")
                                                                                        {
                                                                                            #region Отправка уведомления
                                                                                            string _content = "Ввиду того, что у прибора учета, относящегося к лицевому счету " + tmpExportAccountData.AccountNumber + " закончился межповерочный интервал, предлагаем в срок до " + DateTime.Today.AddDays(60).ToShortDateString() + " выполнить работы по установке поверенного прибора учета с сохранением места его установки, схемы водомерного узла и калибра прибора учета. Записаться на приемку прибора учета можно по тел. 371-57-03, 371-44-43 или на сайте МУП Водоканал https://www.водоканалекб.рф/услуги/электронный-документооборот";
                                                                                            foreach (var itemImportNotificationData in helperHouseManagementService.SetNotificationData(_orgPPAGUID, _FIASHouseGuid, _content, 60).ImportResult.Items)
                                                                                            {
                                                                                                var tmpImportNotificationData = itemImportNotificationData as _HouseManagementService.CommonResultType;
                                                                                                foreach (var tmpCommonResult in tmpImportNotificationData.Items)
                                                                                                {
                                                                                                    if (tmpCommonResult.GetType() == typeof(_HouseManagementService.CommonResultTypeError))
                                                                                                    {
                                                                                                        Console.ForegroundColor = ConsoleColor.Red;
                                                                                                        Console.WriteLine("{0}: {1}", ((_HouseManagementService.CommonResultTypeError)tmpCommonResult).ErrorCode, ((_HouseManagementService.CommonResultTypeError)tmpCommonResult).Description);
                                                                                                        Console.ResetColor();
                                                                                                        _Result.Add(tmpExportAccountData.AccountNumber + "," + ((_HouseManagementService.CommonResultTypeError)tmpCommonResult).ErrorCode + " " + ((_HouseManagementService.CommonResultTypeError)tmpCommonResult).Description);
                                                                                                    }
                                                                                                    else
                                                                                                    {
                                                                                                        Console.WriteLine("Send message about checking of device metering {0}", tmpCommonResult);
                                                                                                        _Result.Add(tmpExportAccountData.AccountNumber + ",Отпралено уведомление о необходимости поверки прибора учета " + tmpCommonResult);
                                                                                                    }
                                                                                                }
                                                                                            }
                                                                                            #endregion
                                                                                        }
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        Console.ForegroundColor = ConsoleColor.Green;
                                                                                        Console.WriteLine("CurrentValue {0} insert with date {1}", _Reading, tmpCommonResultType);
                                                                                        Console.ResetColor();
                                                                                        _Result.Add(tmpExportAccountData.AccountNumber + ",Текущее показание " + _Reading + " внесено " + tmpCommonResultType);
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                        #endregion
                                                                    }
                                                                }
                                                            }
                                                            #endregion
                                                        }
                                                        else
                                                        {
                                                            Console.ForegroundColor = ConsoleColor.DarkRed;
                                                            Console.WriteLine("CurrentDevice in our system is absent");
                                                            Console.ResetColor();
                                                            _Result.Add(tmpExportAccountData.AccountNumber + ",Показания не найдены");
                                                        }
                                                        reader.Close();
                                                        connection.Close();
                                                    }
                                                    #endregion
                                                }
                                                else
                                                {
                                                    Console.ForegroundColor = ConsoleColor.DarkRed;
                                                    Console.WriteLine("AccountData is invalid format");
                                                    Console.ResetColor();
                                                    _Result.Add(tmpExportAccountData.AccountNumber + ",Неверный формат ЛС");
                                                }
                                            }
                                        }
                                        #endregion
                                    }
                                }
                                #endregion
                            }
                            #endregion
                            Console.WriteLine("-----------------------");
                            foreach (var res in _Result)
                            {
                                File.AppendAllText(@"ImportReading.csv", DateTime.Now + "," + res + ";" + Environment.NewLine, Encoding.Default);
                            }
                            _Result.Clear();
                        }
                    }
                    else
                    {
                        _ExportContractRootGUID = tmpSupplyResourceContract.ToString();
                    }
                }
            }
            while (typeRes != "True");
            #endregion
            Console.WriteLine("Import readings complete");
            Console.Read();
        }
    }
}