﻿using AIMLbot;
using AutoMapper;
using BotProject.Common;
using BotProject.Common.ViewModels;
using BotProject.Model.Models;
using BotProject.Service;
using BotProject.Web.Infrastructure.Core;
using BotProject.Web.Models;
using Newtonsoft.Json;
using SearchEngine.Data;
using SearchEngine.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace BotProject.Web.Controllers
{
    [AllowCrossSite]
    public class Apiv1Controller : Controller
    {
        private readonly string UrlAPI = Helper.ReadString("UrlAPI");
        private readonly string KeyAPI = Helper.ReadString("KeyAPI");
        private string pathAIML = PathServer.PathAIML;
        private string pathSetting = PathServer.PathAIML + "config";
        private AccentService _accentService;
        private BotService _botService;
        private ElasticSearch _elastic;
        private IBotService _botDbService;
        private ISettingService _settingService;
        private IHandleModuleServiceService _handleMdService;
        private IModuleService _mdService;
        private IModuleKnowledegeService _mdKnowledgeService;
        private IMdSearchService _mdSearchService;
        private IErrorService _errorService;
        private IAIMLFileService _aimlFileService;
        private IQnAService _qnaService;
        private ApiQnaNLRService _apiNLR;
        //private Bot _bot;
        private User _user;

        public Apiv1Controller(IErrorService errorService,
                                IBotService botDbService,
                                ISettingService settingService,
                                IHandleModuleServiceService handleMdService,
                                IModuleService mdService,
                                IMdSearchService mdSearchService,
                                IModuleKnowledegeService mdKnowledgeService,
                                IAIMLFileService aimlFileService,
                                IQnAService qnaService)
        {
            _errorService = errorService;
            _elastic = new ElasticSearch();
            _accentService = AccentService.AccentInstance;
            _botDbService = botDbService;
            _settingService = settingService;
            _handleMdService = handleMdService;
            _mdService = mdService;
            _apiNLR = new ApiQnaNLRService();
            _mdKnowledgeService = mdKnowledgeService;
            _mdSearchService = mdSearchService;
            _aimlFileService = aimlFileService;
            _qnaService = qnaService;
            _botService = BotService.BotInstance;
        }

        // GET: Apiv1
        public ActionResult Index()
        {
            return View();
        }

        #region CHATBOT
        public ActionResult FormChat(string token, string botId)
        {
            int botID = Int32.Parse(botId);
            var botDb = _botDbService.GetByID(botID);
            var settingDb = _settingService.GetSettingByBotID(botID);
            var settingVm = Mapper.Map<BotProject.Model.Models.Setting, BotSettingViewModel>(settingDb);

            //string nameBotAIML = "User_" + token + "_BotID_" + botId;
            //string fullPathAIML = pathAIML + nameBotAIML;
            //_botService.loadAIMLFromFiles(fullPathAIML);

            var lstAIML = _aimlFileService.GetByBotId(botID);
            var lstAIMLVm = Mapper.Map<IEnumerable<AIMLFile>, IEnumerable<AIMLViewModel>>(lstAIML);
            _botService.loadAIMLFromDatabase(lstAIMLVm);

            UserBotViewModel userBot = new UserBotViewModel();
            userBot.ID = Guid.NewGuid().ToString();
            userBot.BotID = botId;
            _user = _botService.loadUserBot(userBot.ID);

            _user.Predicates.addSetting("phone", "");
            _user.Predicates.addSetting("phonecheck", "false");

            _user.Predicates.addSetting("email", "");
            _user.Predicates.addSetting("emailcheck", "false");

            _user.Predicates.addSetting("age", "");
            _user.Predicates.addSetting("agecheck", "false");

            _user.Predicates.addSetting("name","");
            _user.Predicates.addSetting("namecheck", "false");

            // load tất cả module của bot và thêm key vao predicate
            var mdBotDb = _mdService.GetAllModuleByBotID(botID).Where(x => x.Name != "med_get_info_patient").ToList();
            if (mdBotDb.Count() != 0)
            {
                foreach (var item in mdBotDb)
                {
                    _user.Predicates.addSetting(item.Name, "");
                    _user.Predicates.addSetting(item.Name + "check", "false");
                }
            }
            var lstMdGetInfoPatientDb = _mdKnowledgeService.GetAllMdKnowledgeMedInfPatientByBotID(botID).ToList();
            if (lstMdGetInfoPatientDb.Count() != 0)
            {
                foreach (var item in lstMdGetInfoPatientDb)
                {
                    if (!String.IsNullOrEmpty(item.OptionText))
                    {
                        int index = 0;
                        var arrOpt = item.OptionText.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var opt in arrOpt)
                        {
                            index++;
                            _user.Predicates.addSetting(item.ID + "_opt_" + index, "");
                        }
                    }
                    if (!String.IsNullOrEmpty(item.Payload))
                    {
                        _user.Predicates.addSetting("med_get_info_patient_" + item.ID, "");
                        _user.Predicates.addSetting("med_get_info_patient_check_" + item.ID, "false");
                    }
                }
            }
            var lstMdSearchDb = _mdSearchService.GetByBotID(botID).ToList();
            if (lstMdSearchDb.Count() != 0)
            {
                foreach (var item in lstMdSearchDb)
                {                    
                    if (!String.IsNullOrEmpty(item.Payload))
                    {
                        _user.Predicates.addSetting("api_search" + item.ID, "");//check bat mo truong hop nut' payload khi qua the moi
                        _user.Predicates.addSetting("api_search_check_" + item.ID, "false");
                    }
                }
            }
            _user.Predicates.addSetting("isChkMdGetInfoPatient", "false");
            _user.Predicates.addSetting("ThreadMdGetInfoPatientId", "");
            _user.Predicates.addSetting("isChkMdSearch","false");
            _user.Predicates.addSetting("ThreadMdSearchID", "");
            SettingsDictionaryViewModel settingDic = new SettingsDictionaryViewModel();
            settingDic.Count = _user.Predicates.Count;
            settingDic.orderedKeys = _user.Predicates.orderedKeys;
            settingDic.settingsHash = _user.Predicates.settingsHash;
            settingDic.SettingNames = _user.Predicates.SettingNames;
            userBot.SettingDicstionary = settingDic;
            Session[CommonConstants.SessionUserBot] = userBot;

            return View(settingVm);
        }

        public JsonResult chatbot(string text, string group, string token, string botId, bool isMdSearch)
        {
            //string nameBotAIML = "User_" + token + "_BotID_" + botId;
            //string fullPathAIML = pathAIML + nameBotAIML;
            //_botService.loadAIMLFromFiles(fullPathAIML);
            int valBotID = Int32.Parse(botId);
            try
            {
                if (!String.IsNullOrEmpty(text))
                {
                    text = Regex.Replace(text, @"<(.|\n)*?>", "").Trim();
                }
                if (Session[CommonConstants.SessionUserBot] == null)
                {
                    return Json(new
                    {
                        message = new List<string>() { "Session Timeout" },
                        postback = new List<string>() { null },
                        messageai = "",
                        isCheck = true
                    }, JsonRequestBehavior.AllowGet);
                }
                //get new predicate from session user bot request
                var userBot = (UserBotViewModel)Session[CommonConstants.SessionUserBot];
                _user = _botService.loadUserBot(userBot.ID);
                _user.Predicates.Count = userBot.SettingDicstionary.Count;
                _user.Predicates.SettingNames = userBot.SettingDicstionary.SettingNames;
                _user.Predicates.orderedKeys = userBot.SettingDicstionary.orderedKeys;
                _user.Predicates.settingsHash = userBot.SettingDicstionary.settingsHash;

                // Module lấy thông tin bệnh
                #region Module lấy thông tin bệnh
                // nếu là true
                if (bool.Parse(_user.Predicates.grabSetting("isChkMdGetInfoPatient")))
                {
                    if (text.Contains("module_patient"))
                    {
                        string MdGetInfoPatientId = _user.Predicates.grabSetting("ThreadMdGetInfoPatientId");
                        _user.Predicates.addSetting("med_get_info_patient_check_" + MdGetInfoPatientId, "false");
                        _user.Predicates.addSetting("isChkMdGetInfoPatient", "false");

                        _user.Predicates.addSetting("ThreadMdGetInfoPatientId", "");

                        return chatbot(text, group, token, botId, isMdSearch);
                    }
                    else
                    {
                        string MdGetInfoPatientId = _user.Predicates.grabSetting("ThreadMdGetInfoPatientId");
                        var handlePatient = _handleMdService.HandleIsModuleKnowledgeInfoPatient("postback_module_med_get_info_patient_" + MdGetInfoPatientId + "", valBotID, "Tôi không hiểu, vui lòng chọn lại thông tin bên dưới.");
                        return Json(new
                        {
                            message = new List<string>() { handlePatient.Message },
                            postback = new List<string>() { null },
                            messageai = "",
                            isCheck = true
                        }, JsonRequestBehavior.AllowGet);
                    }
                }
                if (text.Contains("postback_module_med_get_info_patient"))
                {
                    var handlePatient = _handleMdService.HandleIsModuleKnowledgeInfoPatient(text, valBotID, "");

                    string MdGetInfoPatientId = text.Replace("postback_module_med_get_info_patient_", "");
                    _user.Predicates.addSetting("ThreadMdGetInfoPatientId", MdGetInfoPatientId);

                    _user.Predicates.addSetting("med_get_info_patient_check_" + MdGetInfoPatientId, "true");

                    _user.Predicates.addSetting("isChkMdGetInfoPatient", "true");

                    return Json(new
                    {
                        message = new List<string>() { handlePatient.Message },
                        postback = new List<string>() { null },
                        messageai = "",
                        isCheck = true
                    }, JsonRequestBehavior.AllowGet);
                }
                #endregion

                // Module tìm kiếm 
                #region Module Tìm kiếm với api
                if (bool.Parse(_user.Predicates.grabSetting("isChkMdSearch")))
                {
                    if (text.Contains("postback_card"))
                    {
                        _user.Predicates.addSetting("isChkMdSearch", "false");
                        _user.Predicates.addSetting("ThreadMdSearchID", "");
                        return chatbot(text, group, token, botId, isMdSearch);
                    }
                    string mdSearchId = _user.Predicates.grabSetting("ThreadMdSearchID");
                    var handleMdSearch = _handleMdService.HandleIsSearchAPI(text, mdSearchId, "");
                    bool chkAPI = !String.IsNullOrEmpty(handleMdSearch.ResultAPI) == true ? false : true ;                    
                    return Json(new
                    {
                        message = new List<string>() { handleMdSearch.Message },
                        postback = new List<string>() { handleMdSearch.Postback },
                        messageai = handleMdSearch.ResultAPI,
                        isCheck = chkAPI
                    }, JsonRequestBehavior.AllowGet);
                }

                if (text.Contains("postback_module_api_search"))
                {
                    string mdSearchId = text.Replace(".", String.Empty).Replace("postback_module_api_search_", "");
                    var handleMdSearch = _handleMdService.HandleIsSearchAPI(text, mdSearchId, "");
                    _user.Predicates.addSetting("ThreadMdSearchID", mdSearchId);
                    _user.Predicates.addSetting("api_search_check_" + mdSearchId, "true");
                    _user.Predicates.addSetting("isChkMdSearch", "true");
                    return Json(new
                    {
                        message = new List<string>() { handleMdSearch.Message },
                        postback = new List<string>() { null },
                        messageai = "",
                        isCheck = true
                    }, JsonRequestBehavior.AllowGet);
                }
                #endregion

                // Xử lý module phone
                #region 
                bool isCheckPhone = bool.Parse(_user.Predicates.grabSetting("phonecheck"));
                if (isCheckPhone)
                {
                    var handlePhone = _handleMdService.HandleIsPhoneNumber(text, valBotID);
                    if (handlePhone.Status)// đúng số dt
                    {
                        _user.Predicates.addSetting("phonecheck", "false");
                        _user.Predicates.addSetting("phone", text);
                        if (!String.IsNullOrEmpty(handlePhone.Postback))
                        {
                            return chatbot(handlePhone.Postback, group, token, botId, isMdSearch);
                        }
                    }
                    return Json(new
                    {
                        message = new List<string>() { handlePhone.Message },
                        postback = new List<string>() { null },
                        messageai = "",
                        isCheck = true
                    }, JsonRequestBehavior.AllowGet);
                }
                if (text.Contains("postback_module_phone"))
                {
                    string numberPhone = _user.Predicates.grabSetting("phone");
                    _user.Predicates.addSetting("phonecheck", "true");
                    var handlePhone = _handleMdService.HandleIsPhoneNumber(text, valBotID);
                    if (!String.IsNullOrEmpty(numberPhone))// hiển thị số điện thoại nếu đã cung cấp trước đó
                    {
                        string sbPostback = tempNodeBtnModule(numberPhone);

                        return Json(new
                        {
                            message = new List<string>() { handlePhone.Message },
                            postback = new List<string>() { sbPostback.ToString() },
                            messageai = "",
                            isCheck = true
                        }, JsonRequestBehavior.AllowGet);
                    }
                    return Json(new
                    {
                        message = new List<string>() { handlePhone.Message },
                        postback = new List<string>() { null },
                        messageai = "",
                        isCheck = true
                    }, JsonRequestBehavior.AllowGet);
                }
                #endregion

                // Xử lý email
                #region
                bool isCheckMail = bool.Parse(_user.Predicates.grabSetting("emailcheck"));
                if (isCheckMail)
                {
                    var handleEmail = _handleMdService.HandledIsEmail(text, valBotID);
                    if (handleEmail.Status)// đúng email
                    {
                        _user.Predicates.addSetting("emailcheck", "false");
                        _user.Predicates.addSetting("email", text);
                        if (!String.IsNullOrEmpty(handleEmail.Postback))
                        {
                            return chatbot(handleEmail.Postback, group, token, botId, isMdSearch);
                        }
                    }
                    return Json(new
                    {
                        message = new List<string>() { handleEmail.Message },
                        postback = new List<string>() { null },
                        messageai = "",
                        isCheck = true
                    }, JsonRequestBehavior.AllowGet);
                }
                if (text.Contains("postback_module_email"))
                {
                    string email = _user.Predicates.grabSetting("email");
                    _user.Predicates.addSetting("emailcheck", "true");
                    var handleEmail = _handleMdService.HandledIsEmail(text, valBotID);
                    if (!String.IsNullOrEmpty(email))// hiển thị email đã cung cấp trước đó
                    {
                        string sbPostback = tempNodeBtnModule(email);
                        return Json(new
                        {
                            message = new List<string>() { handleEmail.Message },
                            postback = new List<string>() { sbPostback.ToString() },
                            messageai = "",
                            isCheck = true
                        }, JsonRequestBehavior.AllowGet);
                    }
                    return Json(new
                    {
                        message = new List<string>() { handleEmail.Message },
                        postback = new List<string>() { null },
                        messageai = "",
                        isCheck = true
                    }, JsonRequestBehavior.AllowGet);
                }
                #endregion

                // Xử lý age
                #region
                bool isCheckAge = bool.Parse(_user.Predicates.grabSetting("agecheck"));
                if (isCheckAge)
                {
                    var handleAge = _handleMdService.HandledIsAge(text, valBotID);
                    if (handleAge.Status)// đúng age
                    {
                        _user.Predicates.addSetting("agecheck", "false");
                        _user.Predicates.addSetting("age", text);
                        if (!String.IsNullOrEmpty(handleAge.Postback))
                        {
                            return chatbot(handleAge.Postback, group, token, botId, isMdSearch);
                        }
                    }
                    return Json(new
                    {
                        message = new List<string>() { handleAge.Message },
                        postback = new List<string>() { null },
                        messageai = "",
                        isCheck = true
                    }, JsonRequestBehavior.AllowGet);
                }
                if (text.Contains("postback_module_age"))// nếu check đi từ đây trước
                {
                    string age = _user.Predicates.grabSetting("age");
                    _user.Predicates.addSetting("agecheck", "true");
                    var handleAge = _handleMdService.HandledIsAge(text, valBotID);
                    if (!String.IsNullOrEmpty(age))// hiển thị age đã cung cấp trước đó
                    {
                        string sbPostback = tempNodeBtnModule(age);
                        return Json(new
                        {
                            message = new List<string>() { handleAge.Message },
                            postback = new List<string>() { sbPostback.ToString() },
                            messageai = "",
                            isCheck = true
                        }, JsonRequestBehavior.AllowGet);
                    }
                    return Json(new
                    {
                        message = new List<string>() { handleAge.Message },
                        postback = new List<string>() { null },
                        messageai = "",
                        isCheck = true
                    }, JsonRequestBehavior.AllowGet);
                }
                #endregion


                // Lấy target from knowledge base QnA trained
                if(text.Contains("postback") == false || text.Contains("module") == false)
                {
                    string target = _apiNLR.GetPrecidictTextClass(text, valBotID);
                    if (!String.IsNullOrEmpty(target))
                    {
                        target = Regex.Replace(target, "\n", "").Replace("\"", "");
                        QuesTargetViewModel quesTarget = new QuesTargetViewModel();
                        quesTarget = _qnaService.GetQuesByTarget(target, valBotID);
                        if(quesTarget != null)
                        {
                            text = quesTarget.ContentText;
                        }
                    }
                }

                AIMLbot.Result aimlBotResult = _botService.Chat(text, _user);
                string result = aimlBotResult.OutputSentences[0].ToString();
                bool isMatch = true;
                // nếu aiml bot có template trả thẳng ra module k thông qua button text module
                if (result.Replace("\r\n", "").Trim().Contains("postback_module"))
                {
                    if (!result.Contains("<module>"))// k phải button module trả về
                    {
                        string txtModule = result.Replace("\r\n", "").Trim();
                        return chatbot(txtModule, group, token, botId, isMdSearch);
                    }
                }
                // K tìm thấy trong Rule gọi tới module tri thức
                if (result.Contains("NOT_MATCH"))
                {
                    isMatch = false;
                    if (isMdSearch)
                    {
                        result = GetRelatedQuestion(text, group);
                        if (!String.IsNullOrEmpty(result))
                        {
                            var lstQnaAPI = new JavaScriptSerializer
                            {
                                MaxJsonLength = Int32.MaxValue,
                                RecursionLimit = 100
                            }
                            .Deserialize<List<ObjQnaAPI>>(result);
                        }
                        else
                        {
                            //result = NOT_MATCH[res.OutputSentences[0]];
                            result = aimlBotResult.OutputSentences[0].ToString();
                        }
                    }
                }
                //set new predicate to session user bot request
                SettingsDictionaryViewModel settingDic = new SettingsDictionaryViewModel();
                settingDic.Count = aimlBotResult.user.Predicates.Count;
                settingDic.orderedKeys = aimlBotResult.user.Predicates.orderedKeys;
                settingDic.settingsHash = aimlBotResult.user.Predicates.settingsHash;
                settingDic.SettingNames = aimlBotResult.user.Predicates.SettingNames;
                userBot.SettingDicstionary = settingDic;
                Session[CommonConstants.SessionUserBot] = userBot;
                return Json(new
                {
                    message = aimlBotResult.OutputHtmlMessage,
                    postback = aimlBotResult.OutputHtmlPostback,
                    messageai = result,
                    isCheck = isMatch
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                LogError(ex);
                return Json(new
                {
                    message = new List<string>() { "Error" },
                    postback = new List<string>() { null },
                    messageai = "",
                    isCheck = true
                }, JsonRequestBehavior.AllowGet);
            }
        }

        public string tempNodeBtnModule(string valModule)
        {
            StringBuilder sbPostback = new StringBuilder();
            sbPostback.AppendLine("<div class=\"_6biu\">");
            sbPostback.AppendLine("                                                                    <div  class=\"_23n- form_carousel\">");
            sbPostback.AppendLine("                                                                        <div class=\"_4u-c\">");
            sbPostback.AppendLine("                                                                            <div index=\"0\" class=\"_a28 lst_btn_carousel\">");
            sbPostback.AppendLine("                                                                                <div class=\"_a2e\">");
            sbPostback.AppendLine(" <div class=\"_2zgz _2zgz_postback\">");
            sbPostback.AppendLine("      <div class=\"_4bqf _6biq _6bir\" tabindex=\"0\" role=\"button\" data-postback =\"" + valModule + "\" style=\"border-color: {{color}} color: {{color}}\">+ " + valModule + "</div>");
            sbPostback.AppendLine(" </div>");
            sbPostback.AppendLine("                                                                                </div>");
            sbPostback.AppendLine("                                                                            </div>");
            sbPostback.AppendLine("                                                                            <div class=\"_4u-f\">");
            sbPostback.AppendLine("                                                                                <iframe aria-hidden=\"true\" class=\"_1_xb\" tabindex=\"-1\"></iframe>");
            sbPostback.AppendLine("                                                                            </div>");
            sbPostback.AppendLine("                                                                        </div>");
            return sbPostback.ToString();
        }
        
        public List<ObjQnaAPI> GetListQnaByCategory(string category)
        {
            List<ObjQnaAPI> lstQnA = new List<ObjQnaAPI>();
            return lstQnA;
        }

        #endregion

        #region ACCENT VN
        public JsonResult ConvertVN(string text)
        {
            string textVN = "";
            if (!String.IsNullOrEmpty(text))
                text = Regex.Replace(HttpUtility.HtmlDecode(text), @"<(.|\n)*?>", "");
            try
            {
                textVN = _accentService.GetAccentVN(text);
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                return Json(message, JsonRequestBehavior.AllowGet);
            }
            return Json(textVN, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetAccentVN(string text)
        {
            Result rs = new Result();
            if (!String.IsNullOrEmpty(text))
                text = Regex.Replace(HttpUtility.HtmlDecode(text), @"<(.|\n)*?>", "");
            try
            {
                string textVN = _accentService.GetAccentVN(text);
                string arrTextVN = _accentService.GetMultiMatchesAccentVN(text, 5);

                rs.Item = textVN;
                rs.ArrItems = arrTextVN.Split(',').Distinct().Skip(1).ToArray();

            }
            catch (Exception ex)
            {
                string message = ex.Message;
                return Json(message, JsonRequestBehavior.AllowGet);
            }
            return Json(rs, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetMultiMatchesAccentVN(string text)
        {
            string[] ArrItems;
            if (!String.IsNullOrEmpty(text))
                text = Regex.Replace(HttpUtility.HtmlDecode(text), @"<(.|\n)*?>", "");
            try
            {
                string arrTextVN = _accentService.GetMultiMatchesAccentVN(text, 20);
                ArrItems = arrTextVN.Split(',').Distinct().ToArray();

            }
            catch (Exception ex)
            {
                string message = ex.Message;
                return Json(message, JsonRequestBehavior.AllowGet);
            }
            return Json(ArrItems, JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region --SEARCH ENGINE ELASTICSEARCH--
        public JsonResult GetAll(int page = 1, int pageSize = 10)
        {
            int totalRow = 0;
            int from = (page - 1) * pageSize;

            var lstData = _elastic.GetAll(from, pageSize);

            if (lstData.Count() != 0)
            {
                totalRow = lstData[0].Total;
            }

            var paginationSet = new PaginationSet<SearchEngine.Data.Question>()
            {
                Items = lstData,
                Page = page,
                TotalCount = totalRow,
                MaxPage = pageSize,
                TotalPages = (int)Math.Ceiling((decimal)totalRow / pageSize)
            };

            return Json(paginationSet, JsonRequestBehavior.AllowGet);
        }

        public JsonResult Search(string text, bool isAccentVN = false)
        {
            if (!String.IsNullOrEmpty(text))
                text = Regex.Replace(HttpUtility.HtmlDecode(text), @"<(.|\n)*?>", "");

            if (isAccentVN)
            {
                text = _accentService.GetAccentVN(text);
            }

            var lstData = _elastic.Search(text);

            var paginationSet = new PaginationSet<SearchEngine.Data.Question>()
            {
                Items = lstData,
                Page = 1,
                TotalCount = 1,
                TotalPages = 1
            };

            return Json(paginationSet, JsonRequestBehavior.AllowGet);
        }

        public JsonResult Suggest(string text, bool isAccentVN = false)
        {
            if (!String.IsNullOrEmpty(text))
                text = Regex.Replace(HttpUtility.HtmlDecode(text), @"<(.|\n)*?>", "");

            if (isAccentVN)
            {
                text = _accentService.GetAccentVN(text);
            }

            var lstSuggest = _elastic.AutoComplete(text);
            return Json(lstSuggest, JsonRequestBehavior.AllowGet);
        }

        public JsonResult AddQnA(string question, string answer)
        {
            string message = "";
            if (!String.IsNullOrEmpty(question))
            {
                return Json(message, JsonRequestBehavior.AllowGet);
            }
            var result = _elastic.Create(question, answer);
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        //public JsonResult ImportExcelQnA()
        //{
        //	if (Request.Files.Count > 0)
        //		try
        //		{
        //			HttpPostedFileBase file = Request.Files[0];
        //			string pathtempt = Helper.ReadString("ExcelTemplateTemptPath1");
        //			if (!Directory.Exists(pathtempt))
        //			{
        //				Directory.CreateDirectory(pathtempt);
        //			}
        //			string path = System.IO.Path.Combine(pathtempt, file.FileName + "_" + DateTime.Now.Ticks.ToString());
        //			file.SaveAs(path);
        //			DataTable dt = ReadExcelFileToDataTable(path);
        //			return ReadFileExcelQnA(dt);
        //		}
        //		catch (Exception ex)
        //		{
        //			return Json("ERROR:" + ex.Message.ToString());
        //		}
        //	else
        //	{
        //		return Json("Bạn chưa chọn file để tải lên.");
        //	}
        //}

        //private DataTable ReadExcelFileToDataTable(string path)
        //{
        //	string POCpath = @"" + path;
        //	POCpath = POCpath.Replace("\\\\", "\\");
        //	IExcelDataReader dataReader;
        //	FileStream fileStream = new FileStream(POCpath, FileMode.Open);
        //	if (path.EndsWith(".xls"))
        //	{
        //		dataReader = ExcelReaderFactory.CreateBinaryReader(fileStream);
        //	}
        //	else
        //	{
        //		dataReader = ExcelReaderFactory.CreateOpenXmlReader(fileStream);
        //	}
        //	DataSet result = dataReader.AsDataSet();

        //	DataTable dt = result.Tables[0];
        //	return dt;
        //}

        //public JsonResult ReadFileExcelQnA(DataTable dt)
        //{
        //	try
        //	{
        //		if (dt.Rows.Count > 0)
        //		{
        //			var quesList = new List<QuestionViewModel>();
        //			for (var i = 1; i < dt.Rows.Count; i++)
        //			{
        //				var question = new QuestionViewModel();
        //				var item = dt.Rows[i];
        //				question.Id = i;
        //				question.Score = 1;
        //				question.CreationDate = DateTime.Now;
        //				question.Body = item[1] == null ? "" : item[1].ToString();
        //				string mess = "";
        //				bool flag = false;

        //				if (string.IsNullOrEmpty(question.Body))
        //				{
        //					mess += "Câu hỏi không được để trống";
        //					flag = true;
        //				}
        //				if (flag)
        //				{
        //					return Json("File Excel có lỗi ở dòng thứ " + (i + 1) + ":<br/>" + mess);
        //				}

        //				quesList.Add(question);
        //				//CreateQuesForImport("", ques.AreaTitle, ques.ContentsText, ques.AnsContents, null, null, null, null
        //				//, null, null, null);
        //			}
        //			return Json(new { listques = quesList, status = "Thêm dữ liệu thành công!" });
        //		}
        //		else
        //		{
        //			return Json("File không có dữ liệu");
        //		}
        //	}
        //	catch (Exception ex)
        //	{
        //		return Json("Lỗi: " + ex.Message.ToString());
        //	}
        //}
        #endregion

        #region --DATA SOURCE API--

        private string apiRelateQA = "/api/get_related_pairs";

        protected string ApiAddUpdateQA(string NameFuncAPI, object T, string Type = "Post")
        {
            string result = null;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(UrlAPI);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("x-api-key", KeyAPI);
                HttpResponseMessage response = new HttpResponseMessage();
                string json = JsonConvert.SerializeObject(T);            
                StringContent httpContent = new StringContent(json, UnicodeEncoding.UTF8, "application/json");
                try
                {
                    switch (Type)
                    {
                        case "Post":
                            response = client.PostAsync(NameFuncAPI, httpContent).Result;
                            break;
                        case "Get":
                            string requestUri = NameFuncAPI + "?" + httpContent;
                            response = client.GetAsync(requestUri).Result;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    return null;
                }
                if (response.IsSuccessStatusCode)
                {
                    result = response.Content.ReadAsStringAsync().Result;
                }
            }
            return result;
        }
        public string GetRelatedQuestion(string QuestionContent, string GroupQues = "leg")
        {
            var param = new
            {
                question = QuestionContent,
                type = GroupQues,
                number = "10"
            };
            string responseString = ApiAddUpdateQA(apiRelateQA, param, "Post");
            if (responseString != null)
            {
                var lstQues = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 100 }.Deserialize<List<dynamic>>(responseString);
            }
            return responseString;
        }

        #endregion
        public class Result
        {
            public string Item { get; set; }
            public string[] ArrItems { get; set; }
        }

        public class ObjQnaAPI
        {
            public string _id { set; get; }
            public string question { set; get; }
            public string answer { set; get; }
            public string html { set; get; }
            public string field { set; get; }
            public int id { set; get; }
        }

        public UserBotViewModel UserBotInfo
        {
            get
            {
                if (Session != null)
                {
                    if (Session[CommonConstants.SessionUserBot] != null)
                    {
                        return (UserBotViewModel)Session[CommonConstants.SessionUserBot];
                    }
                }
                return null;
            }
            set
            {
                if (value == null)
                    Session.Remove(CommonConstants.SessionUserBot);
                else
                    Session[CommonConstants.SessionUserBot] = value;
            }
        }

        private void LogError(Exception ex)
        {
            try
            {
                BotProject.Model.Models.Error error = new BotProject.Model.Models.Error();
                error.CreatedDate = DateTime.Now;
                error.Message = ex.Message;
                error.StackTrace = ex.StackTrace;
                _errorService.Create(error);
                _errorService.Save();
            }
            catch
            {
            }
        }
    }
}