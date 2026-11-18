<%@ Page Title="My Subjects" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="MySubjects.aspx.vb"
    Inherits="TutorConnect.StudentArea.MySubjects" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">My Subjects - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <div class="form-card" style="margin-bottom:24px">
        <div class="filter-bar">
            <div class="form-row">
                <asp:Label ID="lblSubject" runat="server" AssociatedControlID="ddlSubject">Subject</asp:Label>
                <asp:DropDownList ID="ddlSubject" runat="server" DataTextField="SubjectName" DataValueField="SubjectId" />
            </div>
            <div class="form-row">
                <asp:Label ID="lblLevel" runat="server" AssociatedControlID="ddlStudyLevel">Study level</asp:Label>
                <asp:DropDownList ID="ddlStudyLevel" runat="server" DataTextField="StudyLevelName" DataValueField="StudyLevelId" />
            </div>
            <asp:Button ID="btnAdd" runat="server" Text="Add Subject" CssClass="btn btn--primary" />
        </div>

        <%-- The subject list is shared, so anything added here is available to everybody
             afterwards. The study level comes from the dropdown above. --%>
        <div class="filter-bar">
            <div class="form-row">
                <asp:Label ID="lblNewSubject" runat="server" AssociatedControlID="txtNewSubject">Subject not listed?</asp:Label>
                <asp:TextBox ID="txtNewSubject" runat="server" MaxLength="100"
                             placeholder="e.g. Accounting" />
            </div>
            <asp:Button ID="btnAddNew" runat="server" Text="Add new subject" CssClass="btn btn--outline" />
        </div>

        <p class="form-row__hint">
            Type the full name and choose a study level above. If the subject already exists
            you will be added to the existing one, so please do not abbreviate - a tutor
            teaching "Mathematics" will not be found by a search for "Maths".
        </p>
    </div>

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">My Subjects</h2>
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptSubjects" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Subjects you have added to your profile</caption>
                        <thead>
                            <tr>
                                <th scope="col">Subject</th>
                                <th scope="col">Study Level</th>
                                <th scope="col" class="col-action">Action</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Study Level"><%#: Eval("StudyLevelName") %></td>
                        <td data-label="Action" class="col-action">
                            <asp:LinkButton ID="btnRemove" runat="server" CssClass="btn btn--danger btn--sm"
                                CommandName="Remove" CommandArgument='<%# Eval("StudentSubjectId") %>'
                                OnCommand="Remove_Command"
                                OnClientClick="return confirm('Remove this subject from your list?');">Remove</asp:LinkButton>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                        </tbody>
                    </table>
                </FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">You have not added any subjects yet</p>
                <p>Add a subject and study level above so tutors match what you are studying.</p>
            </asp:Panel>
        </div>
    </div>

</asp:Content>
