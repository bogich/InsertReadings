using System;
using System.ServiceModel;
using Gis.Crypto;
using Gis.Infrastructure.DeviceMeteringService;
using System.Configuration;
using System.Threading;

namespace Gis.Helpers.HelperDeviceMeteringService
{
    /// <summary>
    /// 2.2.8.2	Экспорт сведений о показаниях приборов учета
    /// </summary>
    class HelperDeviceMeteringService
    {

        /// <summary>
        /// Экспорт истории показаний и поверок приборов учета по НСИ2 (Коммунальный ресурс)
        /// </summary>
        /// <param name="_orgPPAGUID">
        /// Идентификатор зарегистрированной организации
        /// </param>
        /// <param name="_FIASHouseGuid">
        /// Глобальный уникальный идентификатор дома по ФИАС
        /// </param>
        /// <param name="_nsiRefCode">
        /// Код записи справочника
        /// </param>
        /// <param name="_nsiRefGUID">
        /// Идентификатор записи в справочнике НСИ2
        /// </param>
        /// <returns></returns>
        public exportMeteringDeviceHistoryResponse GetMeteringDeviceHistory(string _orgPPAGUID, string _FIASHouseGuid, string _nsiRefCode, string _nsiRefGUID)
        {
            var srvDeviceMetering = new DeviceMeteringPortTypesClient();
            srvDeviceMetering.ClientCredentials.UserName.UserName = ConfigurationManager.AppSettings["_login"];
            srvDeviceMetering.ClientCredentials.UserName.Password = ConfigurationManager.AppSettings["_pass"];

            var reqDeviceMeteringExp = new exportMeteringDeviceHistoryRequest1
            {
                RequestHeader = new RequestHeader
                {
                    Date = DateTime.Now,
                    MessageGUID = Guid.NewGuid().ToString(),
                    ItemElementName = ItemChoiceType1.orgPPAGUID,
                    Item = _orgPPAGUID
                },
                exportMeteringDeviceHistoryRequest = new exportMeteringDeviceHistoryRequest
                {
                    version = "10.0.1.1",
                    Id = CryptoConsts.CONTAINER_ID,
                    FIASHouseGuid = _FIASHouseGuid,
                    ItemsElementName = new ItemsChoiceType3[]
                    {
                        ItemsChoiceType3.MunicipalResource //Коммунальный ресурс
                    },
                    Items = new object[]
                    {
                        new nsiRef //Коммунальный ресурс: ХВС
                        {
                            Code = _nsiRefCode,
                            GUID = _nsiRefGUID
                        }
                    },
                    excludeISValuesSpecified = true,
                    excludeISValues = true,
                    SerchArchivedSpecified = true,
                    SerchArchived = true
                }
            };
            
            var resDeviceMeteringExp = srvDeviceMetering.exportMeteringDeviceHistory(reqDeviceMeteringExp);

            return resDeviceMeteringExp;
        }

        /// <summary>
        /// Экспорт истории показаний и поверок приборов учета по идентификатору ПУ
        /// </summary>
        /// <param name="_orgPPAGUID">
        /// Идентификатор зарегистрированной организации
        /// </param>
        /// <param name="_FIASHouseGuid">
        /// Глобальный уникальный идентификатор дома по ФИАС
        /// </param>
        /// <param name="_MeteringDeviceRootGUID">
        /// Идентификатор ПУ
        /// </param>
        /// <returns></returns>
        public exportMeteringDeviceHistoryResponse GetMeteringDeviceHistory(string _orgPPAGUID, string _FIASHouseGuid, string _MeteringDeviceRootGUID)
        {
            var srvDeviceMetering = new DeviceMeteringPortTypesClient();
            srvDeviceMetering.ClientCredentials.UserName.UserName = ConfigurationManager.AppSettings["_login"];
            srvDeviceMetering.ClientCredentials.UserName.Password = ConfigurationManager.AppSettings["_pass"];

            var reqDeviceMeteringExp = new exportMeteringDeviceHistoryRequest1
            {
                RequestHeader = new RequestHeader
                {
                    Date = DateTime.Now,
                    MessageGUID = Guid.NewGuid().ToString(),
                    ItemElementName = ItemChoiceType1.orgPPAGUID,
                    Item = _orgPPAGUID
                },
                exportMeteringDeviceHistoryRequest = new exportMeteringDeviceHistoryRequest
                {
                    version = "10.0.1.1",
                    Id = CryptoConsts.CONTAINER_ID,
                    FIASHouseGuid = _FIASHouseGuid,
                    ItemsElementName = new ItemsChoiceType3[]
                    {
                        ItemsChoiceType3.MeteringDeviceRootGUID //ИД прибора
                    },
                    Items = new object[]
                    {
                        _MeteringDeviceRootGUID //ИД прибора
                    },
                    excludeISValuesSpecified = true,
                    excludeISValues = false,
                    SerchArchivedSpecified = true,
                    SerchArchived = true
                }
            };

            exportMeteringDeviceHistoryResponse resDeviceMeteringExp = null;
            do
            {
                try
                {
                    resDeviceMeteringExp = srvDeviceMetering.exportMeteringDeviceHistory(reqDeviceMeteringExp);
                }
                catch(Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                    Thread.Sleep(1000);

                    if (e.GetType() == typeof(FaultException<Fault>))
                    {
                        //Если превышено время обработки запроса, то меняем его идентификатор, что бы не выполнялось два одинаковых запроса одновременно
                        if (((FaultException<Fault>)e).Detail.ErrorCode.Equals("EXP002002"))
                        {
                            reqDeviceMeteringExp.RequestHeader.MessageGUID = Guid.NewGuid().ToString();
                        }
                    }
                }
            }
            while (resDeviceMeteringExp is null);

            return resDeviceMeteringExp;
        }

        /// <summary>
        /// Передать контрольное показание ПУ
        /// </summary>
        /// <param name="_orgPPAGUID">
        /// Идентификатор зарегистрированной организации
        /// </param>
        /// <param name="_FIASHouseGuid">
        /// Глобальный уникальный идентификатор дома по ФИАС
        /// </param>
        /// <param name="_MeteringDeviceRootGUID">
        /// Идентификатор ПУ
        /// </param>
        /// <param name="_DateValue">
        /// Дата снятия показания
        /// </param>
        /// <param name="_ReadingSource">
        /// Кем внесено
        /// </param>
        /// <param name="_MeteringValue">
        /// Значение
        /// </param>
        /// <returns></returns>
        public importMeteringDeviceValuesResponse SetMeteringDeviceControlValue(string _orgPPAGUID, string _FIASHouseGuid, string _MeteringDeviceRootGUID, DateTime _DateValue, string _MeteringValue)
        {
            var srvDeviceMetering = new DeviceMeteringPortTypesClient();
            srvDeviceMetering.ClientCredentials.UserName.UserName = ConfigurationManager.AppSettings["_login"];
            srvDeviceMetering.ClientCredentials.UserName.Password = ConfigurationManager.AppSettings["_pass"];

            var reqDeviceMeteringImp = new importMeteringDeviceValuesRequest1
            {
                RequestHeader = new RequestHeader
                {
                    Date = DateTime.Now,
                    MessageGUID = Guid.NewGuid().ToString(),
                    ItemElementName = ItemChoiceType1.orgPPAGUID,
                    Item = _orgPPAGUID
                },
                importMeteringDeviceValuesRequest = new importMeteringDeviceValuesRequest
                {
                    version = "10.0.1.1",
                    Id = CryptoConsts.CONTAINER_ID,
                    FIASHouseGuid = _FIASHouseGuid,
                    MeteringDevicesValues = new importMeteringDeviceValuesRequestMeteringDevicesValues[]
                    {
                        new importMeteringDeviceValuesRequestMeteringDevicesValues
                        {
                            ItemElementName = ItemChoiceType.MeteringDeviceRootGUID,
                            Item = _MeteringDeviceRootGUID,
                            Item1 = new importMeteringDeviceValuesRequestMeteringDevicesValuesOneRateDeviceValue
                            {
                                ControlValue = new OneRateMeteringValueImportType[]
                                //CurrentValue = new importMeteringDeviceValuesRequestMeteringDevicesValuesOneRateDeviceValueCurrentValue[]
                                {
                                    new OneRateMeteringValueImportType
                                    //new importMeteringDeviceValuesRequestMeteringDevicesValuesOneRateDeviceValueCurrentValue
                                    {
                                        TransportGUID = Guid.NewGuid().ToString(),
                                        //orgPPAGUID = _orgPPAGUID,
                                        DateValue = _DateValue,
                                        MeteringValue = _MeteringValue,
                                        MunicipalResource = new nsiRef
                                        {
                                            Code = "1",
                                            GUID = "c93bb0cd-0964-4253-a42a-4115130f4cab"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            
            var resMeteringDeviceImp = srvDeviceMetering.importMeteringDeviceValues(reqDeviceMeteringImp);

            return resMeteringDeviceImp;
        }

        /// <summary>
        /// Передать текущее показание ПУ
        /// </summary>
        /// <param name="_orgPPAGUID">
        /// Идентификатор зарегистрированной организации
        /// </param>
        /// <param name="_FIASHouseGuid">
        /// Глобальный уникальный идентификатор дома по ФИАС
        /// </param>
        /// <param name="_MeteringDeviceRootGUID">
        /// Идентификатор ПУ
        /// </param>
        /// <param name="_DateValue">
        /// Дата снятия показания
        /// </param>
        /// <param name="_MeteringValue">
        /// Значение
        /// </param>
        /// <returns></returns>
        public importMeteringDeviceValuesResponse SetMeteringDeviceCurrentlValue(string _orgPPAGUID, string _FIASHouseGuid, string _MeteringDeviceRootGUID, DateTime _DateValue, string _MeteringValue)
        {
            var srvDeviceMetering = new DeviceMeteringPortTypesClient();
            srvDeviceMetering.ClientCredentials.UserName.UserName = ConfigurationManager.AppSettings["_login"];
            srvDeviceMetering.ClientCredentials.UserName.Password = ConfigurationManager.AppSettings["_pass"];

            var reqDeviceMeteringImp = new importMeteringDeviceValuesRequest1
            {
                RequestHeader = new RequestHeader
                {
                    Date = DateTime.Now,
                    MessageGUID = Guid.NewGuid().ToString(),
                    ItemElementName = ItemChoiceType1.orgPPAGUID,
                    Item = _orgPPAGUID
                },
                importMeteringDeviceValuesRequest = new importMeteringDeviceValuesRequest
                {
                    version = "10.0.1.1",
                    Id = CryptoConsts.CONTAINER_ID,
                    FIASHouseGuid = _FIASHouseGuid,
                    MeteringDevicesValues = new importMeteringDeviceValuesRequestMeteringDevicesValues[]
                    {
                        new importMeteringDeviceValuesRequestMeteringDevicesValues
                        {
                            ItemElementName = ItemChoiceType.MeteringDeviceRootGUID,
                            Item = _MeteringDeviceRootGUID,
                            Item1 = new importMeteringDeviceValuesRequestMeteringDevicesValuesOneRateDeviceValue
                            {
                                CurrentValue = new OneRateMeteringValueImportType[]
                                {
                                    new OneRateMeteringValueImportType
                                    {
                                        TransportGUID = Guid.NewGuid().ToString(),
                                        DateValue = _DateValue,
                                        MeteringValue = _MeteringValue,
                                        MunicipalResource = new nsiRef
                                        {
                                            Code = "1",
                                            //GUID = "c93bb0cd-0964-4253-a42a-4115130f4cab" //SIT01
                                            GUID = "82f90cca-24dc-4ff7-ac66-05e53070e5a3" //prod
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            importMeteringDeviceValuesResponse resMeteringDeviceImp = null;
            do
            {
                try
                {
                    resMeteringDeviceImp = srvDeviceMetering.importMeteringDeviceValues(reqDeviceMeteringImp);
                }
                catch(Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                    Thread.Sleep(1000);

                    if (e.GetType() == typeof(FaultException<Fault>))
                    {
                        //Обработка ошибки EXP001000 в случае, когда передача данных не произошла
                        if (((FaultException<Fault>)e).Detail.ErrorCode.Equals("EXP001000"))
                        {
                            reqDeviceMeteringImp.RequestHeader.MessageGUID = Guid.NewGuid().ToString();
                        }
                    }
                }
            }
            while (resMeteringDeviceImp is null);

            return resMeteringDeviceImp;
        }
    }
}
