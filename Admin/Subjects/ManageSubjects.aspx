<%@ Page Title="Manage Subjects" Language="VB" MasterPageFile="~/MasterPages/Admin.Master"
    AutoEventWireup="false" CodeBehind="ManageSubjects.aspx.vb"
    Inherits="TutorConnect.AdminArea.ManageSubjects" Async="true" %>

<%@ Register TagPrefix="tc" TagName="AlertMessage" Src="~/Controls/AlertMessage.ascx" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Manage Subjects - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <tc:AlertMessage ID="alert" runat="server" />

    <div class="panel" style="margin-bottom:24px">
        <div class="panel__header"><h2 class="panel__title">Subjects</h2></div>

        <div class="form-card">
            <div class="filter-bar">
                <div class="form-row">
                    <asp:Label ID="lblNewSubject" runat="server" AssociatedControlID="txtNewSubject">New subject</asp:Label>
                    <asp:TextBox ID="txtNewSubject" runat="server" MaxLength="100" />
                </div>
                <asp:Button ID="btnAddSubject" runat="server" Text="Add Subject" CssClass="btn btn--primary" />
            </div>
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptSubjects" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Subjects offered on the platform</caption>
                        <thead><tr>
                            <th scope="col">Subject</th>
                            <th scope="col">Status</th>
                            <th scope="col">In use</th>
                            <th scope="col" class="col-action">Action</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Status"><%# GetSubjectStatus(Eval("IsActive")) %></td>
                        <td data-label="In use"><%# GetInUse(Eval("SubjectId")) %></td>
                        <td data-label="Action" class="col-action">
                            <asp:LinkButton ID="btnDeactivate" runat="server" CssClass="btn btn--ghost btn--sm"
                                Visible='<%# CBool(Eval("IsActive")) %>'
                                CommandArgument='<%# Eval("SubjectId") %>' OnCommand="Deactivate_Command">Deactivate</asp:LinkButton>
                            <asp:LinkButton ID="btnActivate" runat="server" CssClass="btn btn--outline btn--sm"
                                Visible='<%# Not CBool(Eval("IsActive")) %>'
                                CommandArgument='<%# Eval("SubjectId") %>' OnCommand="Activate_Command">Activate</asp:LinkButton>
                            <asp:LinkButton ID="btnDelete" runat="server" CssClass="btn btn--danger btn--sm"
                                CommandArgument='<%# Eval("SubjectId") %>' OnCommand="Delete_Command"
                                OnClientClick="return confirm('Delete this subject permanently?');">Delete</asp:LinkButton>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
        </div>
    </div>

    <div class="panel">
        <div class="panel__header"><h2 class="panel__title">Study levels</h2></div>

        <div class="form-card">
            <div class="filter-bar">
                <div class="form-row">
                    <asp:Label ID="lblNewLevel" runat="server" AssociatedControlID="txtNewLevel">New study level</asp:Label>
                    <asp:TextBox ID="txtNewLevel" runat="server" MaxLength="50" />
                </div>
                <div class="form-row">
                    <asp:Label ID="lblOrder" runat="server" AssociatedControlID="txtNewOrder">Sort order</asp:Label>
                    <asp:TextBox ID="txtNewOrder" runat="server" TextMode="Number" MaxLength="2" />
                </div>
                <asp:Button ID="btnAddLevel" runat="server" Text="Add Study Level" CssClass="btn btn--primary" />
            </div>
            <p class="form-row__hint">
                Sort order controls how levels appear in dropdowns. Alphabetical order would
                put First Year after Second Year.
            </p>
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptLevels" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Study levels</caption>
                        <thead><tr>
                            <th scope="col">Study level</th>
                            <th scope="col" class="numeric">Sort order</th>
                            <th scope="col">In use</th>
                        </tr></thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Study level"><%#: Eval("StudyLevelName") %></td>
                        <td data-label="Sort order" class="numeric"><%#: Eval("SortOrder") %></td>
                        <td data-label="In use"><%# GetLevelInUse(Eval("StudyLevelId")) %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
        </div>
    </div>

</asp:Content>
