﻿using BotProject.Web.Infrastructure.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using BotProject.Service;
using BotProject.Web.Models;
using BotProject.Model.Models;
using BotProject.Web.Infrastructure.Extensions;
using System.Configuration;
using System.IO;
using System.Text;

namespace BotProject.Web.API
{
    [RoutePrefix("api/qna")]
    public class QnAController : ApiControllerBase
    {
        private IQnAService _qnaService;
        public QnAController(IErrorService errorService, IQnAService qnaService) : base(errorService)
        {
            _qnaService = qnaService;

        }

        [Route("create")]
        [HttpPost]
        public HttpResponseMessage Create(HttpRequestMessage request, QnAnswerGroupViewModel qGroupVm)
        {
            return CreateHttpResponse(request, () =>
            {
                HttpResponseMessage response = null;
                bool IsCreated = false;
                if (qGroupVm != null && qGroupVm.QuestionGroupViewModels != null
                                    && qGroupVm.QuestionGroupViewModels.Count() != 0)
                {
                    var lstQuesGroupVm = qGroupVm.QuestionGroupViewModels;
                    // nếu trường hợp cập nhật chỉ thêm các question group với id rỗng
                    if (qGroupVm.TypeAction == Common.CommonConstants.UpdateQnA)
                    {
                        lstQuesGroupVm = qGroupVm.QuestionGroupViewModels.Where(x => x.ID == 0);
                    }
                    if (lstQuesGroupVm != null && lstQuesGroupVm.Count() != 0)
                    {
                        foreach (var itemQGroup in lstQuesGroupVm)
                        {
                            try
                            {
                                QuestionGroup qGroupDb = new QuestionGroup();
                                qGroupDb.UpdateQuestionGroup(itemQGroup);
                                _qnaService.AddQuesGroup(qGroupDb);
                                _qnaService.Save();
                                if (itemQGroup.QnAViewModel.QuestionViewModels != null &&
                                        itemQGroup.QnAViewModel.QuestionViewModels.Count() != 0)
                                {
                                    var lstQuestion = itemQGroup.QnAViewModel.QuestionViewModels;
                                    foreach (var itemQues in lstQuestion)
                                    {
                                        string code = qGroupDb.ID + Guid.NewGuid().ToString();
                                        Question quesDb = new Question();
                                        quesDb.UpdateQuestion(itemQues);
                                        quesDb.CodeSymbol = code;
                                        quesDb.QuestionGroupID = qGroupDb.ID;
                                        _qnaService.AddQuestion(quesDb);

                                        // is that star
                                        Question quesDbStar = new Question();
                                        quesDbStar.UpdateQuestionIsStar(itemQues);
                                        quesDbStar.CodeSymbol = code;
                                        quesDbStar.QuestionGroupID = qGroupDb.ID;
                                        _qnaService.AddQuestion(quesDbStar);
                                    }

                                    _qnaService.Save();
                                }
                                if (itemQGroup.QnAViewModel.AnswerViewModels != null &&
                                        itemQGroup.QnAViewModel.AnswerViewModels.Count() != 0)
                                {
                                    var lstAnswer = itemQGroup.QnAViewModel.AnswerViewModels;
                                    foreach (var itemAns in lstAnswer)
                                    {
                                        Answer ansDb = new Answer();
                                        ansDb.UpdateAnswer(itemAns);
                                        ansDb.QuestionGroupID = qGroupDb.ID;
                                        _qnaService.AddAnswer(ansDb);
                                    }
                                    _qnaService.Save();
                                }
                                IsCreated = true;
                            }
                            catch (Exception ex)
                            {
                                IsCreated = false;
                                response = request.CreateResponse(HttpStatusCode.BadGateway, IsCreated);
                            }
                        }
                    }
                }
                response = request.CreateResponse(HttpStatusCode.OK, IsCreated);
                return response;
            });
        }

        [Route("getaimlqna")]
        [HttpGet]
        public HttpResponseMessage GetAimlQnA(HttpRequestMessage request, int botQnaID, string botAlias, string userID, int botID)
        {
            return CreateHttpResponse(request, () =>
            {
                HttpResponseMessage response = null;
                var lstQna = _qnaService.GetListQuesGroupToAimlByQnaID(botQnaID).ToList();
                bool IsAiml = false;
                // open file bot aiml
                string pathFolderAIML = ConfigurationManager.AppSettings["AIMLPath"] + "\\" + "User_" + userID + "_BotID_" + botID;
                string nameFolderAIML = "botQnA_ID_" + botQnaID + "_" + botAlias + ".aiml";
                string pathString = System.IO.Path.Combine(pathFolderAIML, nameFolderAIML);
                if (System.IO.File.Exists(pathString))
                {
                    File.WriteAllText(pathString, string.Empty);
                    try
                    {
                        StreamWriter sw = new StreamWriter(pathString, true, Encoding.UTF8);
                        sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                        sw.WriteLine("<aiml>");

                        if (lstQna != null && lstQna.Count() != 0)
                        {
                            int total = lstQna.Count();
                            for (int indexQGroup = 0; indexQGroup < total; indexQGroup++)
                            {
                                var itemQGroup = lstQna[indexQGroup];
                                var lstAnswer = itemQGroup.Answers.ToList();
                                var lstQuestion = itemQGroup.Questions.ToList();
                                string postbackAnswer = String.Empty;
                                if (lstAnswer.Count() != 0 && lstAnswer.Count() > 1)
                                {
                                    StringBuilder sb = new StringBuilder();

                                    int totalAnswer = lstAnswer.Count();
                                    postbackAnswer = "postback_answer_" + itemQGroup.ID;
                                    sb.AppendLine("<category>");
                                    sb.AppendLine("<pattern>"+ postbackAnswer + "</pattern>");
                                    sb.AppendLine("<template>");
                                    sb.AppendLine("<random>");
                                    for (int indexA = 0; indexA < totalAnswer; indexA++)
                                    {
                                        var itemAnswer = lstAnswer[indexA];
                                        if (!String.IsNullOrEmpty(itemAnswer.ContentText))
                                        {
                                            sb.AppendLine("<li>" + itemAnswer.ContentText + "</li>");
                                        }
                                        else
                                        {
                                            sb.AppendLine("<li>" + itemAnswer.CardPayload + "</li>");
                                        }
                                    }
                                    sb.AppendLine("</random>");
                                    sb.AppendLine("</template>");
                                    sb.AppendLine("</category>");
                                    sw.WriteLine(sb.ToString());
                                }
                                else
                                {                                    
                                    if (!String.IsNullOrEmpty(lstAnswer[0].ContentText))
                                    {
                                        postbackAnswer = lstAnswer[0].ContentText;
                                    }
                                    else
                                    {
                                        postbackAnswer = lstAnswer[0].CardPayload;
                                    }
                                }
                                if(lstQuestion.Count != 0)
                                {
                                    for(int indexQ = 0; indexQ < lstQuestion.Count; indexQ++)
                                    {
                                        var itemQ = lstQuestion[indexQ];
                                        string tempAnswer = "";
                                        if (postbackAnswer.Contains("postback"))
                                        {
                                             tempAnswer = "<srai>"+ postbackAnswer+"</srai>";
                                        }else
                                        {
                                            tempAnswer = postbackAnswer;
                                        }
                                        sw.WriteLine("<category>");
                                        sw.WriteLine("<pattern>"+itemQ.ContentText.ToUpper()+"</pattern>");
                                        sw.WriteLine("<template>"+ tempAnswer + "</template>");
                                        sw.WriteLine("</category>");
                                    }
                                }
                            }
                        }
                        sw.WriteLine("</aiml>");
                        sw.Close();
                        IsAiml = true;
                    }
                    catch (Exception ex)
                    {
                        IsAiml = false;
                        response = request.CreateResponse(HttpStatusCode.BadGateway, IsAiml);
                    }
                    finally
                    {

                    }
                }
                response = request.CreateResponse(HttpStatusCode.OK, IsAiml);
                return response;
            });
        }


        [Route("getbybotqnanswerid")]
        [HttpGet]
        public HttpResponseMessage GetByBotQnAnswerId(HttpRequestMessage request, int botQnaID)
        {
            return CreateHttpResponse(request, () =>
            {
                HttpResponseMessage response = null;
                var lstBotQna = _qnaService.GetListQuestionGroupByBotQnAnswerID(botQnaID).OrderByDescending(x => x.CreatedDate).ToList();
                response = request.CreateResponse(HttpStatusCode.OK, lstBotQna);
                return response;
            });
        }

        [Route("deleteques")]
        [HttpPost]
        public HttpResponseMessage DeleteQuestion(HttpRequestMessage request, int questionID)
        {
            return CreateHttpResponse(request, () =>
            {
                HttpResponseMessage response = null;
                var ques = _qnaService.DeleteQuestion(questionID);
                _qnaService.Save();
                response = request.CreateResponse(HttpStatusCode.Created, ques);
                return response;
            });
        }


        [Route("deleteanswer")]
        [HttpPost]
        public HttpResponseMessage DeleteAnswer(HttpRequestMessage request, int answerID)
        {
            return CreateHttpResponse(request, () =>
            {
                HttpResponseMessage response = null;
                var answer = _qnaService.DeleteAnswer(answerID);
                _qnaService.Save();
                response = request.CreateResponse(HttpStatusCode.Created, answer);
                return response;
            });
        }

        [Route("deletequesgroup")]
        [HttpPost]
        public HttpResponseMessage DeleteQuesGroup(HttpRequestMessage request, int qGroupID)
        {
            return CreateHttpResponse(request, () =>
            {
                HttpResponseMessage response = null;
                var qGroup = _qnaService.DeleteQuestionGroup(qGroupID);
                _qnaService.Save();
                response = request.CreateResponse(HttpStatusCode.Created, qGroup);
                return response;
            });
        }

        [Route("updatequestion")]
        [HttpPost]
        public HttpResponseMessage UpdateQuestion(HttpRequestMessage request, QuestionViewModel quesVm)
        {
            return CreateHttpResponse(request, () =>
            {
                HttpResponseMessage response = null;
                if (!ModelState.IsValid)
                {
                    request.CreateErrorResponse(HttpStatusCode.BadRequest, ModelState);
                }
                else
                {
                    try
                    {
                        var _lstQuesUpdate = _qnaService.GetListQuesCodeSymbol(quesVm.CodeSymbol).ToList();
                        if (_lstQuesUpdate.Count != 0)
                        {
                            foreach (var item in _lstQuesUpdate)
                            {
                                if (item.IsThatStar == false)
                                {
                                    item.ContentText = quesVm.ContentText.Trim();
                                }
                                else
                                {
                                    item.ContentText = quesVm.ContentText.Trim() + " *";
                                }
                                _qnaService.UpdateQuestion(item);
                            }
                            _qnaService.Save();
                        }

                        response = request.CreateResponse(HttpStatusCode.OK, "Success");
                    }
                    catch (Exception ex)
                    {
                        response = request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
                    }
                }
                return response;
            });
        }

        [Route("updateanswer")]
        [HttpPost]
        public HttpResponseMessage UpdateAnswer(HttpRequestMessage request, AnswerViewModel answerVm)
        {
            return CreateHttpResponse(request, () =>
            {
                HttpResponseMessage response = null;
                Answer answerDb = new Answer();
                answerDb.UpdateAnswer(answerVm);
                _qnaService.Save();
                response = request.CreateResponse(HttpStatusCode.Created, answerDb);
                return response;
            });
        }

        [Route("addquestion")]
        [HttpPost]
        public HttpResponseMessage AddQuestion(HttpRequestMessage request, QuestionViewModel quesVm)
        {
            return CreateHttpResponse(request, () =>
            {
                HttpResponseMessage response = null;

                string code = quesVm.QuestionGroupID + Guid.NewGuid().ToString();
                Question quesDb = new Question();
                quesDb.UpdateQuestion(quesVm);
                quesDb.CodeSymbol = code;
                quesDb.QuestionGroupID = quesVm.QuestionGroupID;
                _qnaService.AddQuestion(quesDb);

                // is that star
                Question quesDbStar = new Question();
                quesDbStar.UpdateQuestionIsStar(quesVm);
                quesDbStar.CodeSymbol = code;
                quesDbStar.QuestionGroupID = quesVm.QuestionGroupID;
                _qnaService.AddQuestion(quesDbStar);

                _qnaService.Save();
                response = request.CreateResponse(HttpStatusCode.Created, quesDbStar);
                return response;
            });
        }

    }
}