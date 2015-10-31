﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Forms;
using LunchClubMailer.Models;
using LunchClubMailer.Views;

namespace LunchClubMailer
{
    public class LunchClubModel : INotifyPropertyChanged
    {

        #region Binding Properties

        public string subject { get; set; }
        public string organizerEmails { get; set; } //string separated by COMMAS
        public string senderEmail { get; set; }
        public string prefixText { get; set; }
        public string postfixText { get; set; }
        public string host { get; set; }

        // List of members' names
        public List<String> memberList
        {
            get
            {
                List<string> names = new List<string>();
                foreach (LunchClubMember m in file.members)
                {
                    names.Add(m.name);
                }
                return names;
            }
        }
        // Selected member in list
        public String selectedMember { get; set; }
        #endregion

        private AddMemberModel addModel = new AddMemberModel();

        private EditMemberModel editModel = new EditMemberModel();
        
        private SmtpClient smtp = new SmtpClient();

        private LunchClubFile file = LunchClubFile.GetFile();

        public LunchClubModel()
        {
            addModel.requestClose += addModel_requestClose;
            editModel.requestClose += editModel_requestClose;
            if (file.host != null) { smtp.Host = file.host; }
            if (file.subject != null) { subject = file.subject; }
            if (file.prefixText != null) { prefixText = file.prefixText; }
            if (file.postfixText != null) { postfixText = file.postfixText; }
            if (file.senderEmail != null) { senderEmail = file.senderEmail; }
            if (file.organizerEmails != null) { organizerEmails = file.organizerEmails; }
        }

        public void AddMember(object member)
        {
            addModel.Clear();
            AddMember newMemberView = new AddMember(addModel);
            newMemberView.Show();
            PropertyChanged(this, new PropertyChangedEventArgs("memberList"));
        }

        public void DeleteMember()
        {
            if (selectedMember != null)
            {
                file.members.Remove(file.members.First(m => m.name.Equals((string)selectedMember)));
                file.Save();
                PropertyChanged(this, new PropertyChangedEventArgs("memberList"));
            }
            else
            {
                MessageBox.Show("Please select a member to delete first.");
            }

        }

        private void EditMember()
        {
            if (selectedMember != null)
            {
                LunchClubMember member = file.members.First(m => m.name.Equals((string)selectedMember));
                editModel.name = member.name;
                editModel.email = member.email;
                editModel.phoneNumber = member.phoneNumber;
                editModel.diet = member.diet;
                editModel.editMember = member;

                AddMember newMemberView = new AddMember(editModel);
                newMemberView.Show();
                PropertyChanged(this, new PropertyChangedEventArgs("memberList"));
            }
            else
            {
                MessageBox.Show("Please select a member to edit first.");
            }
            
        }

        public List<LunchClubMember> ShuffleMembers(List<LunchClubMember> mems)
        {
            List<LunchClubMember> membersToShuffle = new List<LunchClubMember>();
            List<LunchClubMember> shuffledMembers = new List<LunchClubMember>();
            foreach (LunchClubMember m in mems)
            {
                membersToShuffle.Add(m);
            }
            Random rand = new Random();
            while (membersToShuffle.Count() > 0)
            {
                LunchClubMember mem = membersToShuffle.GetRange(rand.Next(0, membersToShuffle.Count()), 1).FirstOrDefault();

                shuffledMembers.Add(mem);
                membersToShuffle.Remove(mem);
            }

            return shuffledMembers;
        }

        public void SendEmails(object param)
        {
            if(host == null || host.Length == 0)
            {
                MessageBox.Show("Please add an SMTP host. Can be a server name or an IP address.");
            }
            else if (subject == null || subject.Length == 0)
            {
                MessageBox.Show("Please add a subject");
            }
            else if ((prefixText == null || prefixText.Length == 0) && (postfixText == null || postfixText.Length == 0))
            {
                MessageBox.Show("Please add a message to your members. It can come before or after the list of names, or both.");
            }
            else if (senderEmail == null || !addModel.IsValidEmail(senderEmail)) //TODO: move isvalid email out of AddMemberModel
            {
                MessageBox.Show("Please add a valid sender email address.");
            }
            else if (organizerEmails == null || organizerEmails.Length == 0) //TODO: validated organizer emails
            {
                MessageBox.Show("Please add an organizer email address. (Likely the same as your sender, can be more than one.)");
            }
            else
            {
                smtp.Host = host;
                BuildEmails();
                file.host = host;
                file.subject = subject;
                file.prefixText = prefixText;
                file.postfixText = postfixText;
                file.senderEmail = senderEmail;
                file.organizerEmails = organizerEmails;
                file.Save();
                MessageBox.Show("Emails sent!");
            }
        }

        public void BuildEmails()
        {
            string organizerText = "";

            List<LunchClubMember> members = file.members;
            members = ShuffleMembers(members);

            int remainder = members.Count() % 5;
            int numGroups = members.Count() / 5;
            bool groupOfFour = false;
            bool oddGroups = false;

            if (numGroups == 0) //if there's less than 5, just have one group
            {
                numGroups = 1;
                remainder = 0;
            }
            if (remainder == 4) //if there's 4 leftover, there will be one group of 4
            {
                remainder = 0;
                groupOfFour = true;
            }
            if (remainder > 0 && remainder < 4 && numGroups < remainder) //takes care of 7,8, and 13
            {
                //so when I wrote this in Python the math didn't WORK for these three numbers. And these three alone. 
                //you get remainders of 3 which threw off my "groups have to be between 4 and 6 members. 
                //Any number of people with a remainder of 3 being divided by 5 has enough groups to make groups of 6
                //this really was more elegant in Python. Truly. I get why math-y people use it. 
                oddGroups = true;
            }

            if (!oddGroups)//if the groups are all going to be between 4 and 6
            {
                //groups of 6
                for (int i = 0; i < remainder; i++)
                {
                    List<LunchClubMember> group = members.GetRange(6 * i, 6);
                    SendGroupEmail(group);
                    foreach (LunchClubMember member in group)
                    {
                        organizerText += member.name + "\n";
                    }
                    organizerText += "------------------------------------\n";
                }

                //groups of 5
                for (int i = 0; i < numGroups - remainder; i++)
                {
                    List<LunchClubMember> group = members.GetRange(5 * i + 6 * remainder, (memberList.Count < 5 ? memberList.Count: 5));
                    SendGroupEmail(group);
                    foreach (LunchClubMember member in group)
                    {
                        organizerText += member.name + "\n";
                    }
                    organizerText += "------------------------------------\n";
                }

                //group of 4
                if (groupOfFour)
                {
                    List<LunchClubMember> group = members.GetRange(members.Count() - 4, 4);
                    SendGroupEmail(group);
                    foreach (LunchClubMember member in group)
                    {
                        organizerText += member.name + "\n";
                    }
                    organizerText += "------------------------------------\n";
                }
            }
            else //you have a membership of 7,8, or 13 and REALLY need to do some recruiting.
            {
                List<LunchClubMember> group1 = members.GetRange(0, members.Count() / 2); // first half
                SendGroupEmail(group1);
                foreach (LunchClubMember member in group1)
                {
                    organizerText += member.name + "\n";
                }
                organizerText += "------------------------------------\n";

                List<LunchClubMember> group2 = members.GetRange(members.Count() / 2, members.Count() - members.Count() / 2); //second half
                SendGroupEmail(group2);
                foreach (LunchClubMember member in group2)
                {
                    organizerText += member.name + "\n";
                }
                organizerText += "------------------------------------\n";
            }

            //generate organizer email
            EmailOrganizers(organizerText);

        }

        public void SendGroupEmail(List<LunchClubMember> group)
        {
            MailMessage message = new MailMessage();
            string emailText = prefixText + "\n";
            foreach (LunchClubMember member in group)
            {
                emailText += "\n" + member.name;
                if (member.diet == null || member.diet.Length > 0)
                {
                    emailText += ", " + member.diet;
                }
                message.To.Add(member.email);
            }
            emailText += "\n\n" + postfixText;
            message.Body = emailText;

            MailAddress sender = new MailAddress(senderEmail);
            message.From = sender;

            message.Subject = subject;


            smtp.Send(message);
        }

        public void EmailOrganizers(string text)
        {
            MailMessage message = new MailMessage();
            text = "Groups for this month's Lunch Club:\n\n\n" + text;
            message.Body = text;
            message.From = new MailAddress(senderEmail);
            message.To.Add(organizerEmails);
            message.Subject = subject;
            smtp.Send(message);
        }

        private void ExportMemberList()
        {
            ImportExport importExport = new ImportExport(new ImportExportModel());
            importExport.Show();
        }

        private void LaunchHelp()
        {
            HelpWindow helpWindow = new HelpWindow();
            helpWindow.Show();
        }

        #region Commands
        public ICommand SendEmailCommand
        {
            get
            {
                if (_SendEmailCommand == null)
                {
                    _SendEmailCommand = new DelegateCommand(param => this.SendEmails(param));
                }
                return _SendEmailCommand;
            }
        }
        DelegateCommand _SendEmailCommand;

        public ICommand AddCommand
        {
            get
            {
                if (_AddCommand == null)
                {
                    _AddCommand = new DelegateCommand(param => this.AddMember(param));
                }
                return _AddCommand;
            }
        }
        DelegateCommand _AddCommand;

        public ICommand DeleteCommand
        {
            get
            {
                if (_DeleteCommand == null)
                {
                    _DeleteCommand = new DelegateCommand(param => this.DeleteMember());
                }
                return _DeleteCommand;
            }
        }
        DelegateCommand _DeleteCommand;

        public ICommand EditCommand
        {
            get
            {
                if(_EditCommand == null)
                {
                    _EditCommand = new DelegateCommand(param => this.EditMember());
                }
                return _EditCommand;
            }
        }
        DelegateCommand _EditCommand;

        public ICommand LaunchHelpCommand
        {
            get
            {
                if (_LaunchHelpCommand == null)
                {
                    _LaunchHelpCommand = new DelegateCommand(param => this.LaunchHelp());
                }
                return _LaunchHelpCommand;
            }
        }
        DelegateCommand _LaunchHelpCommand;
        

        public ICommand ExportMemberListCommand
        {
            get
            {
                if (_ExportMemberListCommand == null)
                {
                    _ExportMemberListCommand = new DelegateCommand(param => this.ExportMemberList());
                }
                return _ExportMemberListCommand;
            }

        }
        DelegateCommand _ExportMemberListCommand;
        #endregion

        #region EventHandlers
        public event PropertyChangedEventHandler PropertyChanged;

        private void addModel_requestClose(object sender, EventArgs e)
        {
            PropertyChanged(this, new PropertyChangedEventArgs("memberlist"));
        }

        private void editModel_requestClose(object sender, EventArgs e)
        {
            PropertyChanged(this, new PropertyChangedEventArgs("memberlist"));
        }

        #endregion
    }
}
