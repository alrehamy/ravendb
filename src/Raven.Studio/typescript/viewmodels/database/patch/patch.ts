import app = require("durandal/app");
import viewModelBase = require("viewmodels/viewModelBase");
import patchDocument = require("models/database/patch/patchDocument");
import aceEditorBindingHandler = require("common/bindingHelpers/aceEditorBindingHandler");
import getDatabaseStatsCommand = require("commands/resources/getDatabaseStatsCommand");
import collection = require("models/database/documents/collection");
import document = require("models/database/documents/document");
import database = require("models/resources/database");
import messagePublisher = require("common/messagePublisher");
import queryCommand = require("commands/database/query/queryCommand");
import getDocumentWithMetadataCommand = require("commands/database/documents/getDocumentWithMetadataCommand");
import getDocumentsMetadataByIDPrefixCommand = require("commands/database/documents/getDocumentsMetadataByIDPrefixCommand");
import savePatchCommand = require('commands/database/patch/savePatchCommand');
import patchByQueryCommand = require("commands/database/patch/patchByQueryCommand");
import getCustomFunctionsCommand = require("commands/database/documents/getCustomFunctionsCommand");
import queryUtil = require("common/queryUtil");
import getPatchesCommand = require('commands/database/patch/getPatchesCommand');
import eventsCollector = require("common/eventsCollector");
import notificationCenter = require("common/notifications/notificationCenter");
import queryCriteria = require("models/database/query/queryCriteria");
import virtualGridController = require("widgets/virtualGrid/virtualGridController");
import documentBasedColumnsProvider = require("widgets/virtualGrid/columns/providers/documentBasedColumnsProvider");
import executeBulkDocsCommand = require("commands/database/documents/executeBulkDocsCommand");
import popoverUtils = require("common/popoverUtils");
import documentMetadata = require("models/database/documents/documentMetadata");
import deleteDocumentsCommand = require("commands/database/documents/deleteDocumentsCommand");
import columnPreviewPlugin = require("widgets/virtualGrid/columnPreviewPlugin");
import columnsSelector = require("viewmodels/partial/columnsSelector");
import documentPropertyProvider = require("common/helpers/database/documentPropertyProvider");
import textColumn = require("widgets/virtualGrid/columns/textColumn");
import virtualColumn = require("widgets/virtualGrid/columns/virtualColumn");
import patchDocumentCommand = require("commands/database/documents/patchDocumentCommand");
import showDataDialog = require("viewmodels/common/showDataDialog");
import verifyDocumentsIDsCommand = require("commands/database/documents/verifyDocumentsIDsCommand");
import generalUtils = require("common/generalUtils");
import customFunctions = require("models/database/documents/customFunctions");
import evaluationContextHelper = require("common/helpers/evaluationContextHelper");
import collectionsTracker = require("common/helpers/database/collectionsTracker");
import getDocumentsPreviewCommand = require("commands/database/documents/getDocumentsPreviewCommand");

type fetcherType = (skip: number, take: number, previewCols: string[], fullCols: string[]) => JQueryPromise<pagedResult<document>>;

class patchList {

    previewItem = ko.observable<patchDocument>();

    private allPatches = ko.observableArray<patchDocument>([]);

    private readonly useHandler: (patch: patchDocument) => void;
    private readonly removeHandler: (patch: patchDocument) => void;

    hasAnySavedPatch = ko.pureComputed(() => this.allPatches().length > 0);

    previewCode = ko.pureComputed(() => {
        const item = this.previewItem();
        if (!item) {
            return "";
        }

        return Prism.highlight(item.script(), (Prism.languages as any).javascript);
    });

    constructor(useHandler: (patch: patchDocument) => void, removeHandler: (patch: patchDocument) => void) {
        _.bindAll(this, ...["previewPatch", "removePatch", "usePatch"] as Array<keyof this>);
        this.useHandler = useHandler;
        this.removeHandler = removeHandler;
    }

    filteredPatches = ko.pureComputed(() => {
        let text = this.filters.searchText();

        if (!text) {
            return this.allPatches();
        }

        text = text.toLowerCase();

        return this.allPatches().filter(x => x.name().toLowerCase().includes(text));
    });

    filters = {
        searchText: ko.observable<string>()
    };

    previewPatch(item: patchDocument) {
        this.previewItem(item);
    }

    usePatch() {
        this.useHandler(this.previewItem());
    }

    removePatch(item: patchDocument) {
        if (this.previewItem() === item) {
            this.previewItem(null);
        }
        this.removeHandler(item);
    }

    loadAll(db: database) {
        return new getPatchesCommand(db)
            .execute()
            .done((patches: patchDocument[]) => {
                this.allPatches(patches);

                if (this.filteredPatches().length) {
                    this.previewItem(this.filteredPatches()[0]);
                }
            });
    }
}

class patchTester extends viewModelBase {

    testMode = ko.observable<boolean>(false);
    script: KnockoutObservable<string>;
    documentId = ko.observable<string>();
    private db: KnockoutObservable<database>;

    beforeDoc = ko.observable<any>();
    afterDoc = ko.observable<any>();

    actions = {
        loadDocument: ko.observableArray<string>(),
        putDocument: ko.observableArray<any>(),
        info: ko.observableArray<string>()
    };

    showObjectsInPutSection = ko.observable<boolean>(false);

    spinners = {
        testing: ko.observable<boolean>(false),
        loadingDocument: ko.observable<boolean>(false)
    };

    documentIdSearchResults = ko.observableArray<string>([]);

    validationGroup: KnockoutValidationGroup;

    constructor(script: KnockoutObservable<string>, db: KnockoutObservable<database>) {
        super();
        this.script = script;
        this.db = db;
        this.initObservables();

        this.bindToCurrentInstance("closeTestMode", "enterTestMode", "runTest", "onAutocompleteOptionSelected");

        this.validationGroup = ko.validatedObservable({
            script: this.script,
            documentId: this.documentId
        });
    }

    formatAsJson(input: KnockoutObservable<any> | any) {
        return ko.pureComputed(() => {
            const value = ko.unwrap(input);
            if (_.isUndefined(value)) {
                return "";
            } else {
                const json = JSON.stringify(value, null, 4);
                return Prism.highlight(json, (Prism.languages as any).javascript);
            }
        });
    }

    private initObservables() {
        this.testMode.subscribe(testMode => {
            patch.$body.toggleClass('show-test', testMode);
        });

        this.documentId.extend({
            required: true
        });

        this.documentId.throttle(250).subscribe(item => {
            patch.fetchDocumentIdAutocomplete(item, this.db(), this.documentIdSearchResults);
        });

        patch.setupDocumentIdValidation(this.documentId, this.db, () => true);
    }

    closeTestMode() {
        this.testMode(false);
    }

    enterTestMode(documentIdToUse: string) {
        this.testMode(true);
        this.documentId(documentIdToUse);

        this.validationGroup.errors.showAllMessages(false);

        if (documentIdToUse) {
            this.loadDocument();

            if (this.isValid(this.validationGroup, false)) {
                this.runTest();
            }
        }
    }

    resetForm() {
        this.actions.loadDocument([]);
        this.actions.putDocument([]);
        this.actions.info([]);
        this.afterDoc(undefined);
        this.beforeDoc(undefined);
    }

    loadDocument() {
        this.resetForm();

        this.spinners.loadingDocument(true);

        new getDocumentWithMetadataCommand(this.documentId(), this.db())
            .execute()
            .done((doc: document) => {
                if (doc) {
                    this.beforeDoc(doc.toDto(true));
                }
            })
            .fail((xhr: JQueryXHR) => {
                if (xhr.status === 404) {
                    messagePublisher.reportWarning("Document doesn't exist.");
                } else {
                    messagePublisher.reportError("Failed to load document.", xhr.responseText, xhr.statusText);
                }
            })
            .always(() => this.spinners.loadingDocument(false));
    }

    onAutocompleteOptionSelected(item: string) {
        this.documentId(item);
        this.loadDocument();
    }

    runTest(): void {
        eventsCollector.default.reportEvent("patch", "test");

        this.afterAsyncValidationCompleted(this.validationGroup, () => {
            if (this.isValid(this.validationGroup)) {
                this.spinners.testing(true);
                this.resetForm();

                new patchDocumentCommand(this.documentId(), this.script(), true, this.db())
                    .execute()
                    .done((result) => {
                        this.beforeDoc(result.OriginalDocument);
                        this.afterDoc(result.ModifiedDocument);
                        const debug = result.Debug;
                        const actions = debug.Actions as Raven.Server.Documents.Patch.PatchDebugActions;
                        this.actions.loadDocument(actions.LoadDocument);
                        this.actions.putDocument(actions.PutDocument);
                        this.actions.info(debug.Info);

                        if (result.Status === "Patched") {
                            messagePublisher.reportSuccess("Test completed");
                        }
                    })
                    .fail((xhr: JQueryXHR) => {
                        if (xhr.status === 404) {
                            messagePublisher.reportWarning("Test failed: Document doesn't exist.");
                        } else {
                            messagePublisher.reportError("Failed to test patch.", xhr.responseText, xhr.statusText);
                        }
                    })
                    .always(() => this.spinners.testing(false));
            }
        });
    }
}

class patch extends viewModelBase {

    static readonly $body = $("body");
    static readonly ContainerSelector = "#patchContainer";

    inSaveMode = ko.observable<boolean>();
    patchSaveName = ko.observable<string>();

    spinners = {
        save: ko.observable<boolean>(false),
        preview: ko.observable<boolean>(false)
    };

    gridController = ko.observable<virtualGridController<document>>();
    private documentsProvider: documentBasedColumnsProvider;
    private columnPreview = new columnPreviewPlugin<document>();
    columnsSelector = new columnsSelector<document>();
    private fullDocumentsProvider: documentPropertyProvider;
    private fetcher = ko.observable<fetcherType>();

    private customFunctionsContext: object;

    patchDocument = ko.observable<patchDocument>(patchDocument.empty());

    isDocumentMode: KnockoutComputed<boolean>;
    isQueryMode: KnockoutComputed<boolean>;

    documentIdSearchResults = ko.observableArray<string>();

    runPatchValidationGroup: KnockoutValidationGroup;
    runQueryValidationGroup: KnockoutValidationGroup;
    savePatchValidationGroup: KnockoutValidationGroup;
    previewDocumentValidationGroup: KnockoutValidationGroup;

    savedPatches = new patchList(item => this.usePatch(item), item => this.removePatch(item));

    test = new patchTester(this.patchDocument().script, this.activeDatabase);

    private hideSavePatchHandler = (e: Event) => {
        if ($(e.target).closest(".patch-save").length === 0) {
            this.inSaveMode(false);
        }
    };

    constructor() {
        super();
        aceEditorBindingHandler.install();

        this.initValidation();

        this.bindToCurrentInstance("usePatchOption", "previewDocument");
        this.initObservables();
    }

    static setupDocumentIdValidation(field: KnockoutObservable<string>, db: KnockoutObservable<database>, onlyIf: () => boolean) {
        const verifyDocuments = (val: string, params: any, callback: (currentValue: string, result: boolean) => void) => {
            new verifyDocumentsIDsCommand([val], db())
                .execute()
                .done((ids: string[]) => {
                    callback(field(), ids.length > 0);
                });
        };

        field.extend({
            required: true,
            validation: {
                message: "Document doesn't exist.",
                async: true,
                onlyIf: onlyIf,
                validator: generalUtils.debounceAndFunnel(verifyDocuments)
            }
        });
    }

    private initValidation() {
        const doc = this.patchDocument();

        doc.script.extend({
            required: true,
            aceValidation: true
        });
        
        doc.query.extend({
            required: {
                onlyIf: () => this.patchDocument().patchOnOption() === "Query"
            },
            aceValidation: true
        });

        doc.selectedItem.extend({
            required: {
                onlyIf: () => this.patchDocument().patchOnOption() !== "Query"
            }
        });

        patch.setupDocumentIdValidation(doc.selectedItem,
            this.activeDatabase,
            () => this.patchDocument().patchOnOption() === "Document");

        this.patchSaveName.extend({
            required: true
        });

        this.runPatchValidationGroup = ko.validatedObservable({
            script: doc.script,
            selectedItem: doc.selectedItem,
            query: doc.query
        });
        this.runQueryValidationGroup = ko.validatedObservable({
            selectedItem: doc.selectedItem,
            query: doc.query
        });

        this.savePatchValidationGroup = ko.validatedObservable({
            patchSaveName: this.patchSaveName
        });

        this.previewDocumentValidationGroup = ko.validatedObservable({
            selectedItem: doc.selectedItem
        });
    }

    private initObservables() {
        this.isDocumentMode = ko.pureComputed(() => this.patchDocument().patchOnOption() === "Document");
        this.isQueryMode = ko.pureComputed(() => this.patchDocument().patchOnOption() === "Query");

        this.patchDocument().selectedItem.throttle(250).subscribe(item => {
            if (this.patchDocument().patchOnOption() === "Document") {
                patch.fetchDocumentIdAutocomplete(item, this.activeDatabase(), this.documentIdSearchResults);
            }
        });

        this.patchDocument().patchAll.subscribe((patchAll) => {
            this.documentsProvider.showRowSelectionCheckbox = !patchAll;

            this.columnsSelector.reset();
            this.gridController().reset(true);
        });

        this.inSaveMode.subscribe(enabled => {
            const $input = $(".patch-save .form-control");
            if (enabled) {
                $input.show();
                window.addEventListener("click", this.hideSavePatchHandler, true);
            } else {
                this.savePatchValidationGroup.errors.showAllMessages(false);
                window.removeEventListener("click", this.hideSavePatchHandler, true);
                setTimeout(() => $input.hide(), 200);
            }
        });
    }

    activate(recentPatchHash?: string) {
        super.activate(recentPatchHash);
        this.updateHelpLink("QGGJR5");

        this.fullDocumentsProvider = new documentPropertyProvider(this.activeDatabase());

        return $.when<any>(this.fetchCustomFunctions(), this.savedPatches.loadAll(this.activeDatabase()));
    }

    attached() {
        super.attached();

        this.createKeyboardShortcut("ctrl+enter", () => {
            if (this.test.testMode()) {
                this.test.runTest();
            } else {
                this.runPatch();
            }
        }, patch.ContainerSelector);
        
        const jsCode = Prism.highlight("this.NewProperty = this.OldProperty + myParameter;\r\n" +
            "delete this.UnwantedProperty;\r\n" +
            "this.Comments.RemoveWhere(function(comment){\r\n" +
            "  return comment.Spam;\r\n" +
            "});",
            (Prism.languages as any).javascript);

        //TODO: don't use lucene syntax - use RQL
        popoverUtils.longWithHover($(".query-label small"), {
            content: '<p>Queries use Lucene syntax. Examples:</p><pre><span class="token keyword">Name</span>: Hi?berna*<br/><span class="token keyword">Count</span>: [0 TO 10]<br/><span class="token keyword">Title</span>: "RavenDb Queries 1010" <span class="token keyword">AND Price</span>: [10.99 TO *]</pre>'
        });

        popoverUtils.longWithHover($(".patch-title small"),
            {
                content: `<p>Patch Scripts are written in JavaScript. <br />Examples: <pre>${jsCode}</pre></p>`
                + `<p>You can use following functions in your patch script:</p>`
                + `<ul>`
                + `<li><code>PutDocument(documentId, document)</code> - puts document with given name and data</li>`
                + `<li><code>LoadDocument(documentIdToLoad)</code> - loads document by id`
                + `<li><code>output(message)</code> - allows to output debug info when testing patches</li>`
                + `</ul>`
            });
    }

    compositionComplete() {
        super.compositionComplete();

        const grid = this.gridController();
        grid.withEvaluationContext(this.customFunctionsContext);
        this.documentsProvider = new documentBasedColumnsProvider(this.activeDatabase(), grid, collectionsTracker.default.collections().map(x => x.name), {
            showRowSelectionCheckbox: false,
            showSelectAllCheckbox: false,
            createHyperlinks: false,
            customInlinePreview: (doc: document) => this.showPreview(doc),
            enableInlinePreview: true
        });

        const fakeFetcher: fetcherType = () => $.Deferred<pagedResult<document>>().resolve({
            items: [],
            totalResultCount: -1
        });

        grid.headerVisible(true);

        const allColumnsProvider = (results: pagedResultWithAvailableColumns<document>) => {
            const selectedItem = this.patchDocument().selectedItem();
            if (!selectedItem || this.patchDocument().patchOnOption() === "Document" || !this.fetcher()) {
                return [];
            }

            switch (this.patchDocument().patchOnOption()) {
                case "Document":
                    return [];
                case "Query":
                    return documentBasedColumnsProvider.extractUniquePropertyNames(results);
            }
        };

        this.columnsSelector.init(grid, (s, t, previewCols, fullCols) => this.fetcher() ? this.fetcher()(s, t, previewCols, fullCols) : fakeFetcher(s, t, [], []),
            (w, r) => this.documentsProvider.findColumns(w, r),
            allColumnsProvider);

        this.columnPreview.install(".patch-grid", ".tooltip", (doc: document, column: virtualColumn, e: JQueryEventObject, onValue: (context: any) => void) => {
            if (column instanceof textColumn) {
                this.fullDocumentsProvider.resolvePropertyValue(doc, column, (v: any) => {
                    if (!_.isUndefined(v)) {
                        const json = JSON.stringify(v, null, 4);
                        const html = Prism.highlight(json, (Prism.languages as any).javascript);
                        onValue(html);    
                    }
                }, error => {
                    const html = Prism.highlight("Unable to generate column preview: " + error.toString(), (Prism.languages as any).javascript);
                    onValue(html);
                });
            }
        });

        this.fetcher.subscribe(() => grid.reset());
    }

    private showPreview(doc: document) {
        // if document doesn't have all properties fetch them and then display preview

        const meta = doc.__metadata as any;
        const hasCollapsedFields = meta[getDocumentsPreviewCommand.ObjectStubsKey] || meta[getDocumentsPreviewCommand.ArrayStubsKey] || meta[getDocumentsPreviewCommand.TrimmedValueKey];

        if (hasCollapsedFields) {
            new getDocumentWithMetadataCommand(doc.getId(), this.activeDatabase(), true)
                .execute()
                .done((fullDocument: document) => {
                    documentBasedColumnsProvider.showPreview(fullDocument);
                });
        } else {
            // document has all properties - fallback to default method
            documentBasedColumnsProvider.showPreview(doc);
        }
    }

    usePatchOption(option: patchOption) {
        this.fetcher(null);

        const patchDoc = this.patchDocument();
        patchDoc.selectedItem(null);
        patchDoc.patchOnOption(option);
        patchDoc.patchAll(option === "Query");

        if (option !== "Query") {
            patchDoc.query(null);
        }

        this.runPatchValidationGroup.errors.showAllMessages(false);
    }

    usePatch(item: patchDocument) {
        const patchDoc = this.patchDocument();

        patchDoc.copyFrom(item);
    }

    removePatch(item: patchDocument) {
        this.confirmationMessage("Patch", `Are you sure you want to delete patch '${item.name()}'?`, ["Cancel", "Delete"])
            .done(result => {
                if (result.can) {
                    new deleteDocumentsCommand([item.getId()], this.activeDatabase())
                        .execute()
                        .done(() => {
                            messagePublisher.reportSuccess("Deleted patch " + item.name());
                            this.savedPatches.loadAll(this.activeDatabase());
                        })
                        .fail(response => messagePublisher.reportError("Failed to delete " + item.name(), response.responseText, response.statusText));
                }
            });
    }

    runQuery(): void {
        if (this.isValid(this.runQueryValidationGroup)) {
            const database = this.activeDatabase();
            const query = this.patchDocument().query();

            const resultsFetcher = (skip: number, take: number) => {
                const criteria = queryCriteria.empty();
                criteria.queryText(query);

                return new queryCommand(database, skip, take, criteria)
                    .execute();
            };
            this.fetcher(resultsFetcher);
        }
    }

    runPatch() {
        if (this.isValid(this.runPatchValidationGroup)) {

            const patchDoc = this.patchDocument();

            switch (patchDoc.patchOnOption()) {
            case "Document":
                this.patchOnDocuments([patchDoc.selectedItem()]);
                break;
            case "Query":
                if (patchDoc.patchAll()) {
                    this.patchOnQuery();
                } else {
                    const selectedIds = this.gridController().getSelectedItems().map(x => x.getId());
                    this.patchOnDocuments(selectedIds);
                }
                break;
            }
        }
    }

    savePatch() {
        if (this.inSaveMode()) {
            eventsCollector.default.reportEvent("patch", "save");

            if (this.isValid(this.savePatchValidationGroup)) {
                this.spinners.save(true);
                new savePatchCommand(this.patchSaveName(), this.patchDocument(), this.activeDatabase())
                    .execute()
                    .always(() => this.spinners.save(false))
                    .done(() => {
                        this.inSaveMode(false);
                        this.patchSaveName("");
                        this.savePatchValidationGroup.errors.showAllMessages(false);
                        this.savedPatches.loadAll(this.activeDatabase());
                    });
            }
        } else {
            if (this.isValid(this.runPatchValidationGroup)) {
                this.inSaveMode(true);    
            }
        }
    }

    private patchOnDocuments(documentIds: Array<string>) {
        eventsCollector.default.reportEvent("patch", "run", "selected");
        const message = documentIds.length > 1 ? `Are you sure you want to apply this patch to ${documentIds.length} documents?` : 'Are you sure you want to patch document?';

        this.confirmationMessage("Patch", message, ["Cancel", "Patch"])
            .done(result => {
                if (result.can) {
                    const bulkDocs = documentIds.map(docId => ({
                        Id: docId,
                        Type: 'PATCH' as Raven.Client.Documents.Commands.Batches.CommandType,
                        Patch: {
                            Script: this.patchDocument().script()
                        }
                    } as Raven.Server.Documents.Handlers.BatchRequestParser.CommandData));

                    new executeBulkDocsCommand(bulkDocs, this.activeDatabase())
                        .execute()
                        .done(() => messagePublisher.reportSuccess("Patch completed"))
                        .fail((result: JQueryXHR) => messagePublisher.reportError("Unable to patch documents.",
                            result.responseText,
                            result.statusText));
                }
            });
    }

    private patchOnQuery() {
        eventsCollector.default.reportEvent("patch", "run", "query");
        const query = this.patchDocument().query();
        const message = `Are you sure you want to apply this patch to matching documents?`;

        this.confirmationMessage("Patch", message, ["Cancel", "Patch all"])
            .done(result => {
                if (result.can) {
                    const patch = {
                        Script: this.patchDocument().script()
                    } as Raven.Server.Documents.Patch.PatchRequest;

                    new patchByQueryCommand(query, patch, this.activeDatabase())
                        .execute()
                        .done((operationIdDto) => {
                            notificationCenter.instance.openDetailsForOperationById(this.activeDatabase(), operationIdDto.OperationId);
                        });
                }
            });
    }

    static fetchDocumentIdAutocomplete(prefix: string, db: database, output: KnockoutObservableArray<string>) {
        if (prefix && prefix.length > 1) {
            new getDocumentsMetadataByIDPrefixCommand(prefix, 10, db)
                .execute()
                .done(result => {
                    output(result.map(x => x["@metadata"]["@id"]));
                });
        } else {
            output([]);
        }
    }

    private fetchCustomFunctions(): JQueryPromise<customFunctions> {
        return new getCustomFunctionsCommand(this.activeDatabase())
            .execute()
            .done(functions => {
                this.customFunctionsContext = evaluationContextHelper.createContext(functions.functions);
            });
    }

    previewDocument() {
        this.spinners.preview(true);

        this.afterAsyncValidationCompleted(this.previewDocumentValidationGroup, () => {
            if (this.isValid(this.previewDocumentValidationGroup)) {
                new getDocumentWithMetadataCommand(this.patchDocument().selectedItem(), this.activeDatabase())
                    .execute()
                    .done((doc: document) => {
                        const docDto = doc.toDto(true);
                        const metaDto = docDto["@metadata"];
                        documentMetadata.filterMetadata(metaDto);
                        const text = JSON.stringify(docDto, null, 4);
                        app.showBootstrapDialog(new showDataDialog("Document: " + doc.getId(), text, "javascript"));
                    })
                    .always(() => this.spinners.preview(false));
            } else {
                this.spinners.preview(false);
            }
        });
    }

    enterTestMode() {
        const patchDoc = this.patchDocument();

        let documentIdToUse: string = null;
        switch (patchDoc.patchOnOption()) {
            case "Document":
                documentIdToUse = patchDoc.selectedItem();
                break;
            case "Query":
                const selection = this.gridController().getSelectedItems();
                if (selection.length > 0) {
                    documentIdToUse = selection[0].getId();
                }
                break;
        }

        this.test.enterTestMode(documentIdToUse);
    }
}

export = patch;
