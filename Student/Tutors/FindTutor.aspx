<%@ Page Title="Find Tutor" Language="VB" MasterPageFile="~/MasterPages/Student.Master"
    AutoEventWireup="false" CodeBehind="FindTutor.aspx.vb"
    Inherits="TutorConnect.StudentArea.FindTutor" Async="true" %>

<asp:Content ID="T" ContentPlaceHolderID="TitleContent" runat="server">Find Tutor - TutorConnect</asp:Content>

<asp:Content ID="B" ContentPlaceHolderID="MainContent" runat="server">

    <div class="filter-bar">
        <div class="form-row">
            <asp:Label ID="lblSubject" runat="server" AssociatedControlID="ddlSubject">Subject</asp:Label>
            <asp:DropDownList ID="ddlSubject" runat="server" DataTextField="SubjectName" DataValueField="SubjectId" />
        </div>
        <div class="form-row">
            <asp:Label ID="lblLevel" runat="server" AssociatedControlID="ddlStudyLevel">Study Level</asp:Label>
            <asp:DropDownList ID="ddlStudyLevel" runat="server" DataTextField="StudyLevelName" DataValueField="StudyLevelId" />
        </div>
        <asp:Button ID="btnSearch" runat="server" Text="Search" CssClass="btn btn--primary" />
    </div>

    <%-- Qualification and minimum experience are required by the brief but absent from the
         design, so they stay collapsed to keep the visual match. --%>
    <details class="filter-more">
        <summary>More filters</summary>
        <div class="filter-bar" style="margin-top:12px">
            <div class="form-row">
                <asp:Label ID="lblQualification" runat="server" AssociatedControlID="txtQualification">Qualification contains</asp:Label>
                <asp:TextBox ID="txtQualification" runat="server" MaxLength="100" />
            </div>
            <div class="form-row">
                <asp:Label ID="lblExperience" runat="server" AssociatedControlID="txtMinExperience">Minimum experience (years)</asp:Label>
                <asp:TextBox ID="txtMinExperience" runat="server" TextMode="Number" MaxLength="2" />
            </div>
        </div>
    </details>

    <div class="panel">
        <div class="panel__header">
            <h2 class="panel__title">Available Tutors</h2>
        </div>

        <div class="table-responsive">
            <asp:Repeater ID="rptTutors" runat="server">
                <HeaderTemplate>
                    <table class="data-table">
                        <caption class="sr-only">Tutors matching your filters</caption>
                        <thead>
                            <tr>
                                <th scope="col">Tutor Name</th>
                                <th scope="col">Subject</th>
                                <th scope="col">Level</th>
                                <th scope="col">Qualification</th>
                                <th scope="col">Experience</th>
                                <th scope="col" class="numeric">Rate/Session</th>
                                <th scope="col" class="col-action">Action</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td data-label="Tutor Name">
                            <span class="person-cell">
                                <span class="person-cell__avatar" aria-hidden="true"><%# GetInitials(Eval("FullName")) %></span>
                                <span><%#: Eval("FullName") %></span>
                            </span>
                        </td>
                        <td data-label="Subject"><%#: Eval("SubjectName") %></td>
                        <td data-label="Level"><%#: Eval("StudyLevelName") %></td>
                        <td data-label="Qualification"><%#: Eval("Qualification") %></td>
                        <td data-label="Experience"><%# GetExperience(Eval("ExperienceYears")) %></td>
                        <td data-label="Rate/Session" class="numeric"><%# GetRate(Eval("RatePerSession")) %></td>
                        <td data-label="Action" class="col-action">
                            <a class="btn btn--outline btn--sm" href="<%# GetDetailUrl(Eval("TutorSubjectId")) %>">View Details</a>
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                        </tbody>
                    </table>
                </FooterTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <p class="empty-state__title">No tutors match these filters yet</p>
                <p>Try a different subject or level.</p>
                <asp:LinkButton ID="btnClear" runat="server" CssClass="btn btn--outline btn--sm">Clear filters</asp:LinkButton>
            </asp:Panel>
        </div>

        <div class="panel__footer">
            <nav class="pager" aria-label="Search results pages">
                <asp:LinkButton ID="btnPrev" runat="server" CssClass="pager__item" ToolTip="Previous page">&#8249;</asp:LinkButton>
                <%-- PassThrough: the markup is built in code-behind from an integer page
                     number, never from user input. --%>
                <asp:Literal ID="litPage" runat="server" />
                <asp:LinkButton ID="btnNext" runat="server" CssClass="pager__item" ToolTip="Next page">&#8250;</asp:LinkButton>
            </nav>
        </div>
    </div>

</asp:Content>
