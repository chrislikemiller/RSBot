﻿using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Extensions;
using RSBot.Core.Objects.Party;
using RSBot.Core.Objects.Skill;
using RSBot.Theme.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RSBot.Party.Views
{
    [System.ComponentModel.ToolboxItem(false)]
    public partial class Main : UserControl
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        private bool _applySettings = true;

        /// <summary>
        /// The buffing party member list
        /// </summary>
        private List<BuffingPartyMember> _buffings;

        /// <summary>
        /// The selected buffing group
        /// </summary>
        private ListViewItem _selectedBuffingGroup;

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            InitializeComponent();

            selectedMemberBuffs.SmallImageList = Core.Extensions.ListViewExtensions.StaticImageList;
            listPartyBuffSkills.SmallImageList = Core.Extensions.ListViewExtensions.StaticImageList;

            _buffings = new List<BuffingPartyMember>();
            CheckForIllegalCrossThreadCalls = false;
            cbPartySearchPurpose.SelectedIndex = 0;

            SubscribeEvents();
        }

        /// <summary>
        /// Subscribes the events.
        /// </summary>
        private void SubscribeEvents()
        {
            EventManager.SubscribeEvent("OnEnterGame", OnEnterGame);
            EventManager.SubscribeEvent("OnLoadCharacter", OnLoadCharacter);
            EventManager.SubscribeEvent("OnCreatePartyEntry", OnCreatePartyEntry);
            EventManager.SubscribeEvent("OnChangePartyEntry", OnChangePartyEntry);
            EventManager.SubscribeEvent("OnDeletePartyEntry", OnDeletePartyEntry);
            EventManager.SubscribeEvent("OnPartyData", OnPartyData);
            EventManager.SubscribeEvent("OnPartyMemberJoin", new Action<PartyMember>(OnPartyMemberJoin));
            EventManager.SubscribeEvent("OnPartyMemberLeave", new Action<PartyMember>(OnPartyMemberLeave));
            EventManager.SubscribeEvent("OnPartyMemberBanned", new Action<PartyMember>(OnPartyMemberBanned));
            EventManager.SubscribeEvent("OnPartyMemberUpdate", new Action<PartyMember>(OnPartyMemberUpdate));
            EventManager.SubscribeEvent("OnPartyDismiss", OnPartyDismiss);
        }

        /// <summary>
        /// Add new party member
        /// </summary>
        /// <param name="member"></param>
        public void AddNewPartyMember(PartyMember member)
        {
            var viewItem = listParty.Items.Add(member.Name, member.Name, 0);
            viewItem.UseItemStyleForSubItems = false;
            viewItem.Tag = member;

            viewItem.SubItems.Add(member.Level.ToString());
            if (string.IsNullOrWhiteSpace(member.Guild))
                viewItem.SubItems.Add("< No Guild > ").ForeColor = Color.DarkGray;
            else
                viewItem.SubItems.Add(member.Guild);

            var mastery1 = Game.ReferenceManager.GetRefSkillMastery(member.MasteryId1);
            var mastery2 = Game.ReferenceManager.GetRefSkillMastery(member.MasteryId2);
            var location = Game.ReferenceManager.GetTranslation(member.Position.RegionID.ToString());

            var masteryInfo = "<none>";
            if (mastery1 != null)
                masteryInfo = mastery1.Name;
            if (mastery2 != null)
                masteryInfo += $", {mastery2.Name}";

            viewItem.SubItems.Add(masteryInfo);
            viewItem.SubItems.Add(location);
        }

        /// <summary>
        /// Load the buffing groups
        /// </summary>
        /// <returns>The groups</returns>
        private string[] LoadBuffingGroups()
        {
            return PlayerConfig.GetArray<string>("RSBot.Party.Buffing.Groups");
        }

        /// <summary>
        /// Save the buffing groups
        /// </summary>
        private void SaveBuffingGroups()
        {
            PlayerConfig.SetArray("RSBot.Party.Buffing.Groups",
                    listViewGroups.Items.Cast<ListViewItem>().Select(p => p.Text));
        }

        /// <summary>
        /// Get party buffing members from the config
        /// </summary>
        /// <returns>Configured buffed party members</returns>
        private void LoadBuffingMembers()
        {
            var buffingMembers = new List<BuffingPartyMember>();

            var settings = PlayerConfig.Get("RSBot.Party.Buffing", string.Empty);
            var collection = settings.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in collection)
                buffingMembers.Add(new BuffingPartyMember(item));

            _buffings = buffingMembers;
        }

        /// <summary>
        /// Saves the buffing party members
        /// </summary>
        /// <param name="members"></param>
        private void SaveBuffingPartyMembers()
        {
            var stringBuilder = new StringBuilder();
            foreach (var item in _buffings)
                stringBuilder.Append(item.Serialize());

            PlayerConfig.Set("RSBot.Party.Buffing", stringBuilder.ToString());
            PlayerConfig.Save();

            EventManager.FireEvent("OnPartyBuffSettingsChanged");
        }

        /// <summary>
        /// Saves the automatic party player list.
        /// </summary>
        private void SaveAutoPartyPlayerList()
        {
            PlayerConfig.SetArray("RSBot.Party.AutoPartyList", listAutoParty.Items.Cast<string>().ToArray());

            Bundle.Container.Refresh();
        }

        /// <summary>
        /// Refresh the buffing group members
        /// </summary>
        private void RefreshGroupMembers()
        {
            listViewPartyMembers.Items.Clear();

            foreach (ListViewItem itemGroup in listViewGroups.Items)
            {
                var members = _buffings.FindAll(p => p.Group == itemGroup.Text);

                itemGroup.SubItems[1].Text = members.Count.ToString();

                if (itemGroup.Text == _selectedBuffingGroup.Text)
                {
                    foreach (var member in members)
                    {
                        var item = listViewPartyMembers.Items.Add(member.Name, member.Name, 0);
                        if (item.Index == 0)
                            item.Selected = true;
                    }
                }
            }

            LoadPartyBuffSkills();
        }

        /// <summary>
        /// Requests the party list.
        /// </summary>
        private void RequestPartyList(byte page = 0)
        {
            lvPartyMatching.BeginUpdate();
            lvPartyMatching.Items.Clear();
            Task.Run(() =>
            {
                var listViewItems = new List<ListViewItem>();
                var currentPage = Bundle.Container.PartyMatching.RequestPartyList(page);

                btnPrev.Enabled = currentPage.Page > 0;
                btnNext.Enabled = currentPage.Page != currentPage.PageCount - 1;
                btnPrev.Tag = currentPage.Page - 1;
                btnNext.Tag = currentPage.Page + 1;

                lbl_partyPageRange.Text = $"{currentPage.Page + 1} / {currentPage.PageCount}";

                foreach (var party in currentPage.Parties)
                {
                    var existingEntry = listViewItems.Find(p => p.Name == party.Id.ToString());
                    //For a self created party!
                    if (existingEntry != null)
                        continue;

                    var listItem = new ListViewItem { Text = party.Id.ToString(), Name = party.Id.ToString() };
                    listItem.SubItems.Add(party.Race.ToString());
                    listItem.SubItems.Add(party.Leader);
                    listItem.SubItems.Add(party.Title);
                    listItem.SubItems.Add(party.Purpose.ToString());
                    listItem.SubItems.Add(party.MemberCount.ToString("#/" + party.Settings.MaxMember));
                    listItem.SubItems.Add(party.MinLevel + "~" + party.MaxLevel);
                    listItem.ToolTipText = party.Settings.ToString();
                    if (party.Leader == Game.Player.Name ||
                        party.Leader == Game.Player.JobInformation.Name ||
                        (Game.Party?.Leader?.Name == party.Leader))
                    {
                        listItem.Font = new Font(Font, FontStyle.Bold);
                        listItem.BackColor = Color.FromArgb(244, 247, 252);
                        listItem.ForeColor = Color.FromArgb(9, 40, 86);
                        listViewItems.Insert(0, listItem);
                        continue;
                    }
                    listViewItems.Add(listItem);
                }

                foreach (var item in listViewItems)
                    lvPartyMatching.Items.Add(item);
            });
            lvPartyMatching.EndUpdate();
        }

        /// <summary>
        /// The first event that will be fired after the player enters the game
        /// </summary>
        private void OnEnterGame()
        {
            _applySettings = false;

            Bundle.Container.Refresh();

            checkAutoExpAutoShare.Checked = PlayerConfig.Get<bool>("RSBot.Party.EXPAutoShare");
            checkAutoItemAutoShare.Checked = PlayerConfig.Get<bool>("RSBot.Party.ItemAutoShare");
            checkAutoAllowInvitations.Checked = PlayerConfig.Get<bool>("RSBot.Party.AllowInvitations");
            checkAcceptAtTrainingPlace.Checked = Bundle.Container.AutoParty.Config.OnlyAtTrainingPlace;
            checkInviteAll.Checked = Bundle.Container.AutoParty.Config.InviteAll;
            checkInviteFromList.Checked = Bundle.Container.AutoParty.Config.InviteFromList;
            checkAcceptAll.Checked = Bundle.Container.AutoParty.Config.AcceptAll;
            checkAcceptFromList.Checked = Bundle.Container.AutoParty.Config.AcceptFromList;

            listAutoParty.Items.AddRange(PlayerConfig.GetArray<string>("RSBot.Party.AutoPartyList"));
            _applySettings = true;
        }

        /// <summary>
        /// This event will fire as soon as character loaded
        /// </summary>
        private void OnLoadCharacter()
        {
            listViewGroups.Items.Clear();

            var selectedGroup = PlayerConfig.Get("RSBot.Party.Buffing.SelectedGroup", "Default");

            LoadBuffingMembers();
            var groups = LoadBuffingGroups();
            if (groups.Length == 0)
            {
                var item = listViewGroups.Items.Add("Default", "Default", 0);
                item.SubItems.Add("0");
                item.Selected = true;
                _selectedBuffingGroup = item;

                SaveBuffingGroups();
            }
            else
            {
                foreach (var group in groups)
                {
                    var item = listViewGroups.Items.Add(group, group, 0);
                    item.SubItems.Add("0");

                    if (group == selectedGroup)
                    {
                        item.Selected = true;
                        _selectedBuffingGroup = item;
                    }
                }
            }

            RefreshGroupMembers();
        }

        /// <summary>
        /// Load party related buffs
        /// </summary>
        /// <summary>
        /// Loads the skills.
        /// </summary>
        private void LoadPartyBuffSkills()
        {
            if (Game.Player == null)
                return;

            listPartyBuffSkills.BeginUpdate();
            listPartyBuffSkills.Items.Clear();
            listPartyBuffSkills.Groups.Clear();

            foreach (var mastery in Game.Player.Skills.Masteries)
            {
                var group = new ListViewGroup(Game.ReferenceManager.GetTranslation(mastery.Record.NameCode) + " (lv. " + mastery.Level + ")");
                group.Tag = mastery.Id;
                listPartyBuffSkills.Groups.Add(group);
            }

            var skills = Game.Player.Skills.KnownSkills
                    .Where(s => s.Enabled && s.Record.TargetGroup_Party && !s.Record.TargetEtc_SelectDeadBody);

            foreach (var skill in skills)
            {
                if (skill.IsPassive)
                    continue;

                if (checkHideLowerLevelSkills.Checked && skill.IsLowLevel())
                    continue;

                var limit = 0;
                var paramIndex = skill.Record.Params.FindIndex(p => p == 1819175795);

                if (paramIndex != -1)
                    limit = skill.Record.Params[paramIndex + 3];

                var count = _buffings.Count(p => p.Group == _selectedBuffingGroup.Text && p.Buffs.Any(v => v == skill.Id));

                var item = new ListViewItem($"{skill.Record.GetRealName()}");
                item.Tag = skill;

                var subItem = item.SubItems.Add(limit != 0 ? $"{limit - count}" : "No limit");
                item.UseItemStyleForSubItems = false;

                if (limit == 0)
                    subItem.ForeColor = Color.DarkGreen;
                else
                    subItem.ForeColor = Color.DarkRed;

                subItem.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

                foreach (var @group in listPartyBuffSkills.Groups.Cast<ListViewGroup>().Where(@group => Convert.ToInt32(@group.Tag) == skill.Record.ReqCommon_Mastery1))
                    item.Group = @group;

                listPartyBuffSkills.Items.Add(item);
                
                item.LoadSkillImageAsync();
            }

            listPartyBuffSkills.EndUpdate();
        }

        /// <summary>
        /// Displays the party members.
        /// </summary>
        public void OnPartyData()
        {
            listParty.BeginUpdate();
            try
            {
                listParty.Items.Clear();

                if (Game.Party.Members == null)
                {
                    listParty.EndUpdate();
                    OnPartyDismiss();
                    return;
                }

                foreach (var member in Game.Party.Members.FindAll(p => p.Name != Game.Player.Name || p.Name != Game.Player.JobInformation.Name))
                    AddNewPartyMember(member);

                menuBanish.Enabled = Game.Party.IsLeader;

                //Update other UI components
                lblLeader.Text = Game.Party.Leader.Name;
                btnJoinFormedParty.Enabled = false;
                btnPartyMatchForm.Enabled = false;

                _applySettings = false;
                checkCurrentAllowInvitations.Checked = Game.Party.Settings.AllowInvitation;
                checkCurrentAutoShareEXP.Checked = Game.Party.Settings.ExperienceAutoShare;
                checkCurrentAutoShareItems.Checked = Game.Party.Settings.ItemAutoShare;
                _applySettings = true;

                btnLeaveParty.Enabled = true;
                menuLeave.Enabled = true;
            }
            catch
            {
            }
            listParty.EndUpdate();
        }

        private void OnChangePartyEntry()
        {
            Log.Notify($"Formed party changed #{Bundle.Container.PartyMatching.Id}");

            if (tabMain.SelectedTab == tpPartyMatching)
                RequestPartyList();
        }

        private void OnDeletePartyEntry()
        {
            if (tabMain.SelectedTab == tpPartyMatching &&
                lvPartyMatching.Items[0].Name == Bundle.Container.PartyMatching.Id.ToString())
                lvPartyMatching.Items.Remove(lvPartyMatching.Items[0]);

            Bundle.Container.PartyMatching.Id = 0;

            btnPartyMatchForm.Enabled = true;
            btnPartyMatchChangeEntry.Enabled = false;
            btnPartyMatchDeleteEntry.Enabled = false;
            btnJoinFormedParty.Enabled = true;
            grbAutoPartySettings.Enabled = true;

            if (Bundle.Container.PartyMatching.Config.AutoReform)
                Bundle.Container.PartyMatching.Create();
        }

        private void OnCreatePartyEntry()
        {
            Log.Notify($"Formed party matching entry #{Bundle.Container.PartyMatching.Id}");
            btnPartyMatchChangeEntry.Enabled = true;
            btnPartyMatchDeleteEntry.Enabled = true;
            btnPartyMatchForm.Enabled = false;
            btnJoinFormedParty.Enabled = false;
            grbAutoPartySettings.Enabled = false;

            _applySettings = false;
            checkCurrentAllowInvitations.Checked = Game.Party.Settings.AllowInvitation;
            checkCurrentAutoShareEXP.Checked = Game.Party.Settings.ExperienceAutoShare;
            checkCurrentAutoShareItems.Checked = Game.Party.Settings.ItemAutoShare;
            _applySettings = true;

            if (tabMain.SelectedTab == tpPartyMatching)
                RequestPartyList();
        }

        private void OnPartyMemberJoin(PartyMember member)
        {
            Log.Notify($"[{member.Name}] joined the party.");
            AddNewPartyMember(member);
        }

        private void OnPartyMemberLeave(PartyMember member)
        {
            Log.Notify($"[{member.Name}] has left the party.");
            listParty.Items.RemoveByKey(member.Name);
        }

        private void OnPartyMemberBanned(PartyMember member)
        {
            Log.Notify($"[{member.Name}] is banned from the party.");
            if (member.Name != Game.Player.Name)
                listParty.Items.RemoveByKey(member.Name);
            else
                OnPartyDismiss();
        }

        /// <summary>
        /// Called when [party member update].
        /// </summary>
        /// <param name="member">The member.</param>
        private void OnPartyMemberUpdate(PartyMember member)
        {
            if (!listParty.Items.ContainsKey(member.Name))
                return;

            var lvItem = listParty.Items[member.Name];

            lvItem.Text = member.Name;
            lvItem.SubItems[1].Text = member.Level.ToString();
            lvItem.SubItems[2].Text = member.Guild;

            var mastery1 = Game.ReferenceManager.GetRefSkillMastery(member.MasteryId1);
            var mastery2 = Game.ReferenceManager.GetRefSkillMastery(member.MasteryId2);
            var location = Game.ReferenceManager.GetTranslation(member.Position.RegionID.ToString());

            var masteryInfo = "<none>";
            if (mastery1 != null)
                masteryInfo = mastery1.Name;
            if (mastery2 != null)
                masteryInfo += $", {mastery2.Name}";

            lvItem.SubItems[3].Text = masteryInfo;
            lvItem.SubItems[4].Text = location;
        }

        /// <summary>
        /// Clear party
        /// </summary>
        public void OnPartyDismiss()
        {
            Bundle.Container.PartyMatching.HasMatchingEntry = false;
            btnLeaveParty.Enabled = false;
            menuLeave.Enabled = false;
            lblLeader.Text = "<Not in a party>";
            listParty.Items.Clear();
            Log.Notify("The party has been dismissed!");
            OnDeletePartyEntry();
        }

        /// <summary>
        /// Handles the Click event of the btnLeaveParty control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void btnLeaveParty_Click(object sender, System.EventArgs e)
        {
            Game.Party.Leave();
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkSettings control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void checkSettings_CheckedChanged(object sender, System.EventArgs e)
        {
            if (!_applySettings)
                return;

            PlayerConfig.Set("RSBot.Party.EXPAutoShare", checkAutoExpAutoShare.Checked);
            PlayerConfig.Set("RSBot.Party.ItemAutoShare", checkAutoItemAutoShare.Checked);
            PlayerConfig.Set("RSBot.Party.AllowInvitations", checkAutoAllowInvitations.Checked);

            Bundle.Container.AutoParty.Refresh();
        }

        /// <summary>
        /// Handles the Click event of the menuBanish control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void menuBanish_Click(object sender, System.EventArgs e)
        {
            if (listParty.SelectedItems.Count == 1 && Game.Party.IsLeader)
                Game.Party.GetMemberByName(listParty.SelectedItems[0].Text)?.Banish();
        }

        /// <summary>
        /// Handles the Click event of the btnAddToAutoParty control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void btnAddToAutoParty_Click(object sender, System.EventArgs e)
        {
            var diag = new InputDialog("Input", "Character name", "Please enter the character name that you want to add to the auto party list.");

            if (diag.ShowDialog() != DialogResult.OK) return;

            listAutoParty.Items.Add(diag.Value);
            SaveAutoPartyPlayerList();
        }

        /// <summary>
        /// Handles the Click event of the btnRemoveFromAutoParty control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void btnRemoveFromAutoParty_Click(object sender, System.EventArgs e)
        {
            if (listAutoParty.SelectedIndex == -1) return;
            listAutoParty.Items.Remove(listAutoParty.SelectedItem);

            SaveAutoPartyPlayerList();
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkAutoPartySetting control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void checkAutoPartySetting_CheckedChanged(object sender, System.EventArgs e)
        {
            if (!_applySettings) return;

            PlayerConfig.Set("RSBot.Party.AcceptAll", checkAcceptAll.Checked);
            PlayerConfig.Set("RSBot.Party.AcceptList", checkAcceptFromList.Checked);
            PlayerConfig.Set("RSBot.Party.InviteAll", checkInviteAll.Checked);
            PlayerConfig.Set("RSBot.Party.InviteList", checkInviteFromList.Checked);
            PlayerConfig.Set("RSBot.Party.AtTrainingPlace", checkAcceptAtTrainingPlace.Checked);
            PlayerConfig.Set("RSBot.Party.AcceptIfBotStopped", checkAcceptIfBotStopped.Checked);

            Bundle.Container.Refresh();
        }

        /// <summary>
        /// Handles the Click event of the ctxJoinParty control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void btnJoinFormedParty_Click(object sender, System.EventArgs e)
        {
            if (lvPartyMatching.SelectedItems.Count != 1)
                return;

            var partyNumber = Convert.ToInt32(lvPartyMatching.SelectedItems[0].Text);

            Log.Notify("Requesting to join party #" + partyNumber);

            Task.Run(() =>
            {
                btnJoinFormedParty.Enabled = false;
                btnJoinFormedParty.Text = "Joining...";

                var joiningResult = Bundle.Container.PartyMatching.Join(partyNumber);
                if (joiningResult)
                    RequestPartyList();

                btnJoinFormedParty.Text = "Join Party";
                btnJoinFormedParty.Enabled = !joiningResult;
            });
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the tpPartyMatching page.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void tabMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabMain.SelectedTab == tpPartyMatching)
                RequestPartyList();
        }

        /// <summary>
        /// Handles the Click event of the btnPartyMatchForm control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnPartyMatchForm_Click(object sender, EventArgs e)
        {
            var senderName = (sender as Button).Name;

            var formingParty = new AutoFormParty(senderName == nameof(btnPartyMatchForm) ? "Forming party" : "Change Entry");
            if (formingParty.ShowDialog() == DialogResult.OK)
            {
                if (senderName == nameof(btnPartyMatchForm))
                    Bundle.Container.PartyMatching.Create();
                else
                    Bundle.Container.PartyMatching.Change();
            }
        }

        private void PageNatigateBtn_Click(object sender, EventArgs e)
        {
            var btn = sender as Button;
            btn.Enabled = false;

            RequestPartyList(Convert.ToByte(btn.Tag));
        }

        private void btnPartyMatchDeleteEntry_Click(object sender, EventArgs e)
        {
            Bundle.Container.PartyMatching.Config.AutoReform = false;
            Bundle.Container.PartyMatching.Delete();
        }

        private void btnPartyRefresh_Click(object sender, EventArgs e)
        {
            RequestPartyList(Convert.ToByte(lbl_partyPageRange.Tag));
        }

        private void btnPartySearch_Click(object sender, EventArgs e)
        {
            var lvItems = lvPartyMatching.Items.Cast<ListViewItem>().ToList();

            var selectedPurpose = cbPartySearchPurpose.SelectedItem.ToString();

            if (selectedPurpose != "All")
                lvItems.RemoveAll(p => p.SubItems[4].Text != selectedPurpose);

            if (!string.IsNullOrWhiteSpace(tbPartySearchName.Text))
            {
                //No case sensitivity
                lvItems.RemoveAll(p => !p.SubItems[2].Text.ToLowerInvariant().Contains(tbPartySearchName.Text.ToLowerInvariant()));
            }

            if (nudPartySearchMin.Value > 1 || nudPartySearchMax.Value < 140)
            {
                lvItems.RemoveAll(p => p.SubItems[6].Text != nudPartySearchMin.Value + "~" + nudPartySearchMax.Value);

                if (selectedPurpose != "All")
                    lvItems.RemoveAll(p => p.SubItems[4].Text != selectedPurpose);
            }

            if (lvItems?.Count() > 0 && lvItems?.Count() != lvPartyMatching.Items.Count)
            {
                lvPartyMatching.Items.Clear();

                lvPartyMatching.Items.AddRange(lvItems.ToArray());
            }
        }

        private void checkHideLowerLevelSkills_CheckedChanged(object sender, EventArgs e)
        {
            LoadPartyBuffSkills();
            PlayerConfig.Set("RSBot.Party.Buffing.HideLowerLevelSkills", checkHideLowerLevelSkills.Checked);
        }

        private void listViewGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewGroups.SelectedIndices.Count == 0 ||
                listViewGroups.SelectedIndices[0] == _selectedBuffingGroup.Index)
                return;

            _selectedBuffingGroup = listViewGroups.SelectedItems[0];

            PlayerConfig.Set("RSBot.Party.Buffing.SelectedGroup", _selectedBuffingGroup.Text);
            RefreshGroupMembers();
        }

        private void listViewPartyMembers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewPartyMembers.SelectedItems.Count == 0)
                return;

            selectedMemberBuffs.Items.Clear();

            var name = listViewPartyMembers.SelectedItems[0].Text;
            var member = _buffings.Find(p => p.Name == name);
            if (member == null)
                return;

            foreach (var skillId in member.Buffs)
            {
                var listViewItemOfMainList = listPartyBuffSkills.Items.Cast<ListViewItem>()
                    .FirstOrDefault(p => ((ISkillDataInfo)p.Tag).Id == skillId);
                if (listViewItemOfMainList == null)
                    continue;

                var clone = (ListViewItem)listViewItemOfMainList.Clone();
                clone.SubItems.RemoveAt(1);
                selectedMemberBuffs.Items.Add(clone);
            }
        }

        private void btnAddBuffToMember_Click(object sender, EventArgs e)
        {
            if (listViewPartyMembers.SelectedItems.Count == 0)
                return;

            if (listPartyBuffSkills.SelectedItems.Count == 0)
                return;

            var memberName = listViewPartyMembers.SelectedItems[0].Text;

            var buffingMember = _buffings.Find(p => p.Name == memberName);
            if (buffingMember == null)
                return;

            foreach (ListViewItem viewItem in listPartyBuffSkills.SelectedItems)
            {
                var skill = viewItem.Tag as SkillInfo;
                if (skill == null)
                    continue;

                if (buffingMember.Buffs.Any(id => id == skill.Id || 
                    (Game.ReferenceManager.SkillData.TryGetValue(id, out var refSkill) && 
                    refSkill.Action_Overlap == skill.Record.Action_Overlap)))
                    continue;

                buffingMember.Buffs.Add(skill.Id);

                var buffItem = (ListViewItem)viewItem.Clone();
                buffItem.SubItems.RemoveAt(1);
                selectedMemberBuffs.Items.Add(buffItem);

                var subItem = viewItem.SubItems[1];
                if (int.TryParse(subItem.Text, out var limit))
                    subItem.Text = (limit - 1).ToString();
            }

            SaveBuffingPartyMembers();
        }

        private void btnRemoveBuffFromMember_Click(object sender, EventArgs e)
        {
            if (listViewPartyMembers.SelectedItems.Count == 0)
                return;

            if (selectedMemberBuffs.SelectedItems.Count == 0)
                return;

            var memberName = listViewPartyMembers.SelectedItems[0].Text;

            var buffingMember = _buffings.FirstOrDefault(p => p.Name == memberName);
            if (buffingMember == null)
                return;

            foreach (ListViewItem viewItem in selectedMemberBuffs.SelectedItems)
            {
                var skill = viewItem.Tag as SkillInfo;
                if (skill == null)
                    continue;

                buffingMember.Buffs.Remove(skill.Id);
                viewItem.Remove();

                var mainItem = listPartyBuffSkills.Items.Cast<ListViewItem>().FirstOrDefault(p => p.Tag == skill);
                if (mainItem == null)
                    continue;

                var subItem = mainItem.SubItems[1];
                if (int.TryParse(subItem.Text, out var limit))
                    subItem.Text = (limit + 1).ToString();
            }

            SaveBuffingPartyMembers();
        }

        private void menuItemAddToBuffing_Click(object sender, EventArgs e)
        {
            if (listParty.SelectedItems.Count == 0)
                return;

            var partyMember = listParty.SelectedItems[0].Tag as PartyMember;
            if (partyMember == null)
                return;

            if (partyMember.Name == Game.Player.Name)
                return;

            var dialogTitle = "Select a group for [{partyMember.Name}]";
            var dialog = new InputDialog(dialogTitle,dialogTitle, "Select the group for add member to the selected group!", InputDialog.InputType.Combobox);

            var groups = listViewGroups.Items.Cast<ListViewItem>()
                                             .Select(p => p.Text)
                                             .ToArray();

            dialog.Selector.Items.AddRange(groups);

            dialog.Selector.SelectedIndex = 0;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var dialogValue = dialog.Value.ToString();

                if (_buffings.Any(p => p.Group == dialogValue && p.Name == partyMember.Name))
                {
                    MessageBox.Show($"The {partyMember.Name} already in the buffing list. You can add a buff on a [Buffing] tab");
                    return;
                }

                _buffings.Add(new BuffingPartyMember
                {
                    Name = partyMember.Name,
                    Group = dialogValue
                });

                if (dialogValue == _selectedBuffingGroup.Text)
                    RefreshGroupMembers();

                SaveBuffingPartyMembers();

                MessageBox.Show("Successfully added the party member to buffing. You can configure the party member on the [Buffing] tab.");
            }
        }

        private void buttonAddGroup_Click(object sender, EventArgs e)
        {
            var dialog = new InputDialog("Create new group", "Create new group", "Please enter the group name to the input.");
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var value = dialog.Value.ToString();
                var item = listViewGroups.Items.Add(value, value, 0);
                item.SubItems.Add("0");
                item.Selected = true;

                SaveBuffingGroups();
            }
        }

        private void buttonRemoveGroup_Click(object sender, EventArgs e)
        {
            if (listViewGroups.SelectedItems.Count == 0)
                return;

            var selectedItem = listViewGroups.SelectedItems[0];
            var group = selectedItem.Text;

            var count = _buffings.Count(p => p.Group == group);
            if (count == 0)
            {
                selectedItem.Remove();

                SaveBuffingGroups();

                return;
            }

            if (MessageBox.Show(this, $"The group keeping {count} members! Are you sure for delete the group?",
                "WARNING", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                if (group != "Default")
                    selectedItem.Remove();

                var affected = _buffings.RemoveAll(p => p.Group == group) > 0;

                SaveBuffingGroups();
                SaveBuffingPartyMembers();

                if (affected)
                    LoadPartyBuffSkills();
            }
        }

        private void buttonRemoveCharFromBuffing_Click(object sender, EventArgs e)
        {
            if (listViewPartyMembers.SelectedItems.Count == 0)
                return;

            if (MessageBox.Show(this, $"It will delete the character from the list. Are you sure about that?",
                "WARNING", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var selectedItem = listViewPartyMembers.SelectedItems[0];
                var name = selectedItem.Text;

                var affected = _buffings.RemoveAll(p => p.Name == name);
                if (affected > 0)
                {
                    selectedItem.Remove();
                    RefreshGroupMembers();
                    SaveBuffingPartyMembers();
                    LoadPartyBuffSkills();
                }
            }
        }
    }
}