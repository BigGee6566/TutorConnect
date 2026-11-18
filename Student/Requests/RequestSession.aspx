<%@ Page Title="Request Session" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="RequestSession.aspx.vb"
    Inherits="TutorConnect.StudentArea.RequestSession" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Request Session - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <asp:Panel ID="pnlForm" runat="server" CssClass="form-card">

        <dl class="fact-list">
            <div class="fact-list__row"><dt>Tutor</dt><dd><asp:Literal ID="litTutor" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Subject</dt><dd><asp:Literal ID="litSubject" runat="server" Mode="Encode" /></dd></div>
            <div class="fact-list__row"><dt>Rate/Session</dt><dd><asp:Literal ID="litRate" runat="server" Mode="Encode" /></dd></div>
        </dl>

        <div class="form-row">
            <span class="field-legend" id="slotLabel">Choose a time slot</span>
            <%-- Radio buttons, not a dropdown: the choice is visible at a glance and the
                 form cannot be submitted without one being selected. --%>
            <asp:RadioButtonList ID="rblSlots" runat="server" CssClass="slot-list"
                                 DataTextField="Display" DataValueField="AvailabilityId"
                                 aria-labelledby="slotLabel" />
        </div>

        <div class="form-row">
            <asp:Label ID="lblMessage" runat="server" AssociatedControlID="txtMessage">Message to the tutor (optional)</asp:Label>
            <asp:TextBox ID="txtMessage" runat="server" TextMode="MultiLine" MaxLength="500"
                         placeholder="Anything the tutor should know before the session?" />
            <p class="form-row__hint">500 characters maximum.</p>
        </div>

        <div class="form-actions">
            <asp:Button ID="btnSubmit" runat="server" Text="Submit Request" CssClass="btn btn--success" />
            <asp:HyperLink ID="lnkCancel" runat="server" CssClass="btn btn--ghost">Cancel</asp:HyperLink>
        </div>

    </asp:Panel>

</asp:Content>
