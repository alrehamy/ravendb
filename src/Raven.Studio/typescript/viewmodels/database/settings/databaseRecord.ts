import appUrl = require("common/appUrl");
import aceEditorBindingHandler = require("common/bindingHelpers/aceEditorBindingHandler");
import getDatabaseSettingsCommand = require("commands/resources/getDatabaseSettingsCommand");
import document = require("models/database/documents/document");
import database = require("models/resources/database");
import viewModelBase = require("viewmodels/viewModelBase");
import messagePublisher = require("common/messagePublisher");
import accessHelper = require("viewmodels/shell/accessHelper");
import eventsCollector = require("common/eventsCollector");

class databaseRecord extends viewModelBase {
    document = ko.observable<document>();
    documentText = ko.observable<string>().extend({ required: true });
    docEditor: AceAjax.Editor;
    securedSettings: string;
    updatedDto: documentDto;
    leavePageDeffered: JQueryPromise<any>;
    isForbidden = ko.observable<boolean>(false);

    static containerId ="#databaseSettingsContainer";

    constructor() {
        super();
        aceEditorBindingHandler.install();

        this.document.subscribe(doc => {
            if (doc) {
                const docDto: any = doc.toDto();
                this.securedSettings = ko.toJSON(docDto.SecuredSettings);
                const docText = this.stringify(doc.toDto());
                this.documentText(docText);
            }
        });
    }

    canActivate(args: any) {
        super.canActivate(args);
        var deferred = $.Deferred();

        this.isForbidden(!accessHelper.isGlobalAdmin());
        if (this.isForbidden()) {
            deferred.resolve({ can: true });
        } else {
            var db: database = this.activeDatabase();
            this.fetchDatabaseSettings(db)
                .done(() => deferred.resolve({ can: true }))
                .fail((response: JQueryXHR) => {
                    messagePublisher.reportError("Error fetching database settings!", response.responseText, response.statusText);
                    deferred.resolve({ redirect: appUrl.forStatus(db) });
                });
        }

        return deferred;
    }

    activate(args: any) {
        super.activate(args);
        this.updateHelpLink('3QMLGH');
    }

    compositionComplete() {
        super.compositionComplete();

        var editorElement = $("#dbDocEditor");
        if (editorElement.length > 0)
        {
            this.docEditor = ko.utils.domData.get(editorElement[0], "aceEditor");
        }
        $("#dbDocEditor").on('DynamicHeightSet', () => this.docEditor.resize());
    }

    detached() {
        super.detached();
        $("#dbDocEditor").off('DynamicHeightSet');
    }


    refreshFromServer() {
        eventsCollector.default.reportEvent("database-settings", "refresh");

        this.fetchDatabaseSettings(this.activeDatabase(), true);
    }

    private fetchDatabaseSettings(db: database, reportFetchProgress: boolean = false): JQueryPromise<any> {
        return new getDatabaseSettingsCommand(db, reportFetchProgress)
            .execute()
            .done((document: document) => this.document(document));
    }

    private getDatabaseSettingsDocumentId(db: database) {
        return "Raven/Databases/" + db.name;
    }

    private stringify(obj: any) {
        const prettifySpacing = 4;
        return JSON.stringify(obj, (key, val) => {
            // strip out null properties
            return _.isNull(val) || _.isEqual(val, {}) ? undefined : val;
        }, prettifySpacing);
    }
}

export = databaseRecord;
