﻿using Penguin.Cms.Email;
using Penguin.Cms.Email.Abstractions;
using Penguin.Cms.Email.Repositories;
using Penguin.Cms.Errors;
using Penguin.Cms.Logging;
using Penguin.Email.Services;
using Penguin.Messaging.Core;
using Penguin.Messaging.Logging.Extensions;
using Penguin.Persistence.Abstractions.Interfaces;
using Penguin.Workers.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Cms.Workers.Email
{
    /// <summary>
    /// This worker retrieves unsent emails from the EmailRepository that are scheduled to go out, and attempts to send them through a mail service
    /// </summary>
    public class EmailWorker : CmsWorker
    {
        /// <summary>
        /// The intended amount of time between runs
        /// </summary>
        public override TimeSpan Delay => new(0, 0, 5, 0, 0);

        /// <summary>
        /// The email repository to use when interacting with the email queue
        /// </summary>
        protected EmailRepository EmailRepository { get; set; }

        /// <summary>
        /// The mail service to use when sending emails
        /// </summary>
        protected MailService Mail { get; set; }

        /// <summary>
        /// Constructs a new instance of this email worker
        /// </summary>
        /// <param name="emailRepository">The email repository to use when interacting with the email queue</param>
        /// <param name="mail">The mail service to use when sending emails</param>
        /// <param name="workerRepository">A worker repository to record relevant worker information in</param>
        /// <param name="logEntryRepository">A log entry repository to log out run-time messages</param>
        /// <param name="errorRepository">An error repository to record any exceptions</param>
        /// <param name="messageBus">An optional message bus to send out messages related to the state of this worker</param>
        public EmailWorker(EmailRepository emailRepository, MailService mail, WorkerRepository workerRepository, IRepository<LogEntry> logEntryRepository, IRepository<AuditableError> errorRepository, MessageBus messageBus = null) : base(workerRepository, logEntryRepository, errorRepository, messageBus)
        {
            EmailRepository = emailRepository;
            Mail = mail;
        }

        /// <summary>
        /// Executes the email worker, attempting to send the next 100 messages in the queue
        /// </summary>
        public override void RunWorker(params string[] args)
        {
            List<EmailMessage> unsentMessages = EmailRepository.GetScheduledEmails(100).ToList();

            foreach (EmailMessage thisMessage in unsentMessages)
            {
                Logger.LogInfo("Sending message {0}", thisMessage.Guid);

                EmailRepository.UpdateState(thisMessage._Id, EmailMessageState.Failure);

                try
                {
                    Mail.Send(thisMessage);

                    thisMessage.State = EmailMessageState.Success;

                    EmailRepository.UpdateState(thisMessage._Id, EmailMessageState.Success);
                }
                catch (Exception ex)
                {
                    MessageBus?.Log(ex);
                    thisMessage.State = EmailMessageState.Failure;
                    throw;
                }
            }
        }
    }
}