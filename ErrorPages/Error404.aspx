<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="Error404.aspx.vb"
    Inherits="TutorConnect.ErrorPages.Error404" %>

<!DOCTYPE html>
<html lang="en-ZA">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Page not found - TutorConnect</title>
    <link rel="icon" href="<%: ResolveUrl("~/Images/favicon.png") %>" type="image/png" />
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/themes/colors.css") %>" />
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/themes/typography.css") %>" />
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/css/site.css") %>" />
    <link rel="stylesheet" href="<%: ResolveUrl("~/Content/css/errors.css") %>" />
</head>
<body class="app-page error-page">
    <main class="error-panel">
        <p class="error-panel__code">404</p>
        <h1 class="error-panel__title">We can't find that page</h1>
        <p class="error-panel__text">
            The page you asked for doesn't exist, or it may have moved.
        </p>

        <%-- Shown only while the application is being built out, so a link to a screen
             that has not been written yet reads as work-in-progress rather than a fault. --%>
        <asp:Panel ID="pnlUnderConstruction" runat="server" CssClass="alert alert--info" role="status" Visible="false">
            This part of TutorConnect has not been built yet. It is planned for a later
            phase of the project.
        </asp:Panel>

        <div class="error-panel__actions">
            <asp:HyperLink ID="lnkHome" runat="server" CssClass="btn btn--primary">Go to my dashboard</asp:HyperLink>
            <asp:HyperLink ID="lnkLogin" runat="server" NavigateUrl="~/Account/Login.aspx"
                           CssClass="btn btn--outline">Back to login</asp:HyperLink>
        </div>
    </main>
</body>
</html>
