(function(a,A){typeof exports=="object"&&typeof module<"u"?A(exports):typeof define=="function"&&define.amd?define(["exports"],A):(a=typeof globalThis<"u"?globalThis:a||self,A(a.OdiseaComponents={}))})(this,function(a){"use strict";var St;const A={ai:"All inclusive",fb:"Пълен пансион",hb:"Полупансион",bb:"Закуска",ro:"Без изхранване",AllInclusive:"All inclusive",FullBoard:"Пълен пансион",HalfBoard:"Полупансион",BedAndBreakfast:"Закуска",RoomOnly:"Без изхранване"},V={beach:"Морска почивка",tour:"Обиколен тур",city:"Градски уикенд",cruise:"Круиз",honey:"Меден месец",safari:"Сафари"};/**
 * @license
 * Copyright 2019 Google LLC
 * SPDX-License-Identifier: BSD-3-Clause
 */const X=globalThis,et=X.ShadowRoot&&(X.ShadyCSS===void 0||X.ShadyCSS.nativeShadow)&&"adoptedStyleSheets"in Document.prototype&&"replace"in CSSStyleSheet.prototype,it=Symbol(),lt=new WeakMap;let pt=class{constructor(t,e,i){if(this._$cssResult$=!0,i!==it)throw Error("CSSResult is not constructable. Use `unsafeCSS` or `css` instead.");this.cssText=t,this.t=e}get styleSheet(){let t=this.o;const e=this.t;if(et&&t===void 0){const i=e!==void 0&&e.length===1;i&&(t=lt.get(e)),t===void 0&&((this.o=t=new CSSStyleSheet).replaceSync(this.cssText),i&&lt.set(e,t))}return t}toString(){return this.cssText}};const Pt=o=>new pt(typeof o=="string"?o:o+"",void 0,it),x=(o,...t)=>{const e=o.length===1?o[0]:t.reduce((i,r,s)=>i+(n=>{if(n._$cssResult$===!0)return n.cssText;if(typeof n=="number")return n;throw Error("Value passed to 'css' function must be a 'css' function result: "+n+". Use 'unsafeCSS' to pass non-literal values, but take care to ensure page security.")})(r)+o[s+1],o[0]);return new pt(e,o,it)},Et=(o,t)=>{if(et)o.adoptedStyleSheets=t.map(e=>e instanceof CSSStyleSheet?e:e.styleSheet);else for(const e of t){const i=document.createElement("style"),r=X.litNonce;r!==void 0&&i.setAttribute("nonce",r),i.textContent=e.cssText,o.appendChild(i)}},ht=et?o=>o:o=>o instanceof CSSStyleSheet?(t=>{let e="";for(const i of t.cssRules)e+=i.cssText;return Pt(e)})(o):o;/**
 * @license
 * Copyright 2017 Google LLC
 * SPDX-License-Identifier: BSD-3-Clause
 */const{is:zt,defineProperty:Bt,getOwnPropertyDescriptor:Tt,getOwnPropertyNames:Mt,getOwnPropertySymbols:jt,getPrototypeOf:It}=Object,S=globalThis,ft=S.trustedTypes,Dt=ft?ft.emptyScript:"",rt=S.reactiveElementPolyfillSupport,U=(o,t)=>o,J={toAttribute(o,t){switch(t){case Boolean:o=o?Dt:null;break;case Object:case Array:o=o==null?o:JSON.stringify(o)}return o},fromAttribute(o,t){let e=o;switch(t){case Boolean:e=o!==null;break;case Number:e=o===null?null:Number(o);break;case Object:case Array:try{e=JSON.parse(o)}catch{e=null}}return e}},ot=(o,t)=>!zt(o,t),ut={attribute:!0,type:String,converter:J,reflect:!1,useDefault:!1,hasChanged:ot};Symbol.metadata??(Symbol.metadata=Symbol("metadata")),S.litPropertyMetadata??(S.litPropertyMetadata=new WeakMap);let D=class extends HTMLElement{static addInitializer(t){this._$Ei(),(this.l??(this.l=[])).push(t)}static get observedAttributes(){return this.finalize(),this._$Eh&&[...this._$Eh.keys()]}static createProperty(t,e=ut){if(e.state&&(e.attribute=!1),this._$Ei(),this.prototype.hasOwnProperty(t)&&((e=Object.create(e)).wrapped=!0),this.elementProperties.set(t,e),!e.noAccessor){const i=Symbol(),r=this.getPropertyDescriptor(t,i,e);r!==void 0&&Bt(this.prototype,t,r)}}static getPropertyDescriptor(t,e,i){const{get:r,set:s}=Tt(this.prototype,t)??{get(){return this[e]},set(n){this[e]=n}};return{get:r,set(n){const p=r==null?void 0:r.call(this);s==null||s.call(this,n),this.requestUpdate(t,p,i)},configurable:!0,enumerable:!0}}static getPropertyOptions(t){return this.elementProperties.get(t)??ut}static _$Ei(){if(this.hasOwnProperty(U("elementProperties")))return;const t=It(this);t.finalize(),t.l!==void 0&&(this.l=[...t.l]),this.elementProperties=new Map(t.elementProperties)}static finalize(){if(this.hasOwnProperty(U("finalized")))return;if(this.finalized=!0,this._$Ei(),this.hasOwnProperty(U("properties"))){const e=this.properties,i=[...Mt(e),...jt(e)];for(const r of i)this.createProperty(r,e[r])}const t=this[Symbol.metadata];if(t!==null){const e=litPropertyMetadata.get(t);if(e!==void 0)for(const[i,r]of e)this.elementProperties.set(i,r)}this._$Eh=new Map;for(const[e,i]of this.elementProperties){const r=this._$Eu(e,i);r!==void 0&&this._$Eh.set(r,e)}this.elementStyles=this.finalizeStyles(this.styles)}static finalizeStyles(t){const e=[];if(Array.isArray(t)){const i=new Set(t.flat(1/0).reverse());for(const r of i)e.unshift(ht(r))}else t!==void 0&&e.push(ht(t));return e}static _$Eu(t,e){const i=e.attribute;return i===!1?void 0:typeof i=="string"?i:typeof t=="string"?t.toLowerCase():void 0}constructor(){super(),this._$Ep=void 0,this.isUpdatePending=!1,this.hasUpdated=!1,this._$Em=null,this._$Ev()}_$Ev(){var t;this._$ES=new Promise(e=>this.enableUpdating=e),this._$AL=new Map,this._$E_(),this.requestUpdate(),(t=this.constructor.l)==null||t.forEach(e=>e(this))}addController(t){var e;(this._$EO??(this._$EO=new Set)).add(t),this.renderRoot!==void 0&&this.isConnected&&((e=t.hostConnected)==null||e.call(t))}removeController(t){var e;(e=this._$EO)==null||e.delete(t)}_$E_(){const t=new Map,e=this.constructor.elementProperties;for(const i of e.keys())this.hasOwnProperty(i)&&(t.set(i,this[i]),delete this[i]);t.size>0&&(this._$Ep=t)}createRenderRoot(){const t=this.shadowRoot??this.attachShadow(this.constructor.shadowRootOptions);return Et(t,this.constructor.elementStyles),t}connectedCallback(){var t;this.renderRoot??(this.renderRoot=this.createRenderRoot()),this.enableUpdating(!0),(t=this._$EO)==null||t.forEach(e=>{var i;return(i=e.hostConnected)==null?void 0:i.call(e)})}enableUpdating(t){}disconnectedCallback(){var t;(t=this._$EO)==null||t.forEach(e=>{var i;return(i=e.hostDisconnected)==null?void 0:i.call(e)})}attributeChangedCallback(t,e,i){this._$AK(t,i)}_$ET(t,e){var s;const i=this.constructor.elementProperties.get(t),r=this.constructor._$Eu(t,i);if(r!==void 0&&i.reflect===!0){const n=(((s=i.converter)==null?void 0:s.toAttribute)!==void 0?i.converter:J).toAttribute(e,i.type);this._$Em=t,n==null?this.removeAttribute(r):this.setAttribute(r,n),this._$Em=null}}_$AK(t,e){var s,n;const i=this.constructor,r=i._$Eh.get(t);if(r!==void 0&&this._$Em!==r){const p=i.getPropertyOptions(r),h=typeof p.converter=="function"?{fromAttribute:p.converter}:((s=p.converter)==null?void 0:s.fromAttribute)!==void 0?p.converter:J;this._$Em=r;const g=h.fromAttribute(e,p.type);this[r]=g??((n=this._$Ej)==null?void 0:n.get(r))??g,this._$Em=null}}requestUpdate(t,e,i,r=!1,s){var n;if(t!==void 0){const p=this.constructor;if(r===!1&&(s=this[t]),i??(i=p.getPropertyOptions(t)),!((i.hasChanged??ot)(s,e)||i.useDefault&&i.reflect&&s===((n=this._$Ej)==null?void 0:n.get(t))&&!this.hasAttribute(p._$Eu(t,i))))return;this.C(t,e,i)}this.isUpdatePending===!1&&(this._$ES=this._$EP())}C(t,e,{useDefault:i,reflect:r,wrapped:s},n){i&&!(this._$Ej??(this._$Ej=new Map)).has(t)&&(this._$Ej.set(t,n??e??this[t]),s!==!0||n!==void 0)||(this._$AL.has(t)||(this.hasUpdated||i||(e=void 0),this._$AL.set(t,e)),r===!0&&this._$Em!==t&&(this._$Eq??(this._$Eq=new Set)).add(t))}async _$EP(){this.isUpdatePending=!0;try{await this._$ES}catch(e){Promise.reject(e)}const t=this.scheduleUpdate();return t!=null&&await t,!this.isUpdatePending}scheduleUpdate(){return this.performUpdate()}performUpdate(){var i;if(!this.isUpdatePending)return;if(!this.hasUpdated){if(this.renderRoot??(this.renderRoot=this.createRenderRoot()),this._$Ep){for(const[s,n]of this._$Ep)this[s]=n;this._$Ep=void 0}const r=this.constructor.elementProperties;if(r.size>0)for(const[s,n]of r){const{wrapped:p}=n,h=this[s];p!==!0||this._$AL.has(s)||h===void 0||this.C(s,void 0,n,h)}}let t=!1;const e=this._$AL;try{t=this.shouldUpdate(e),t?(this.willUpdate(e),(i=this._$EO)==null||i.forEach(r=>{var s;return(s=r.hostUpdate)==null?void 0:s.call(r)}),this.update(e)):this._$EM()}catch(r){throw t=!1,this._$EM(),r}t&&this._$AE(e)}willUpdate(t){}_$AE(t){var e;(e=this._$EO)==null||e.forEach(i=>{var r;return(r=i.hostUpdated)==null?void 0:r.call(i)}),this.hasUpdated||(this.hasUpdated=!0,this.firstUpdated(t)),this.updated(t)}_$EM(){this._$AL=new Map,this.isUpdatePending=!1}get updateComplete(){return this.getUpdateComplete()}getUpdateComplete(){return this._$ES}shouldUpdate(t){return!0}update(t){this._$Eq&&(this._$Eq=this._$Eq.forEach(e=>this._$ET(e,this[e]))),this._$EM()}updated(t){}firstUpdated(t){}};D.elementStyles=[],D.shadowRootOptions={mode:"open"},D[U("elementProperties")]=new Map,D[U("finalized")]=new Map,rt==null||rt({ReactiveElement:D}),(S.reactiveElementVersions??(S.reactiveElementVersions=[])).push("2.1.2");/**
 * @license
 * Copyright 2017 Google LLC
 * SPDX-License-Identifier: BSD-3-Clause
 */const R=globalThis,gt=o=>o,Y=R.trustedTypes,bt=Y?Y.createPolicy("lit-html",{createHTML:o=>o}):void 0,vt="$lit$",P=`lit$${Math.random().toFixed(9).slice(2)}$`,mt="?"+P,Ht=`<${mt}>`,z=document,N=()=>z.createComment(""),q=o=>o===null||typeof o!="object"&&typeof o!="function",st=Array.isArray,Lt=o=>st(o)||typeof(o==null?void 0:o[Symbol.iterator])=="function",at=`[ 	
\f\r]`,F=/<(?:(!--|\/[^a-zA-Z])|(\/?[a-zA-Z][^>\s]*)|(\/?$))/g,yt=/-->/g,_t=/>/g,B=RegExp(`>|${at}(?:([^\\s"'>=/]+)(${at}*=${at}*(?:[^ 	
\f\r"'\`<>=]|("|')|))|$)`,"g"),$t=/'/g,xt=/"/g,wt=/^(?:script|style|textarea|title)$/i,Ut=o=>(t,...e)=>({_$litType$:o,strings:t,values:e}),c=Ut(1),H=Symbol.for("lit-noChange"),l=Symbol.for("lit-nothing"),Ot=new WeakMap,T=z.createTreeWalker(z,129);function kt(o,t){if(!st(o)||!o.hasOwnProperty("raw"))throw Error("invalid template strings array");return bt!==void 0?bt.createHTML(t):t}const Rt=(o,t)=>{const e=o.length-1,i=[];let r,s=t===2?"<svg>":t===3?"<math>":"",n=F;for(let p=0;p<e;p++){const h=o[p];let g,v,u=-1,C=0;for(;C<h.length&&(n.lastIndex=C,v=n.exec(h),v!==null);)C=n.lastIndex,n===F?v[1]==="!--"?n=yt:v[1]!==void 0?n=_t:v[2]!==void 0?(wt.test(v[2])&&(r=RegExp("</"+v[2],"g")),n=B):v[3]!==void 0&&(n=B):n===B?v[0]===">"?(n=r??F,u=-1):v[1]===void 0?u=-2:(u=n.lastIndex-v[2].length,g=v[1],n=v[3]===void 0?B:v[3]==='"'?xt:$t):n===xt||n===$t?n=B:n===yt||n===_t?n=F:(n=B,r=void 0);const E=n===B&&o[p+1].startsWith("/>")?" ":"";s+=n===F?h+Ht:u>=0?(i.push(g),h.slice(0,u)+vt+h.slice(u)+P+E):h+P+(u===-2?p:E)}return[kt(o,s+(o[e]||"<?>")+(t===2?"</svg>":t===3?"</math>":"")),i]};class K{constructor({strings:t,_$litType$:e},i){let r;this.parts=[];let s=0,n=0;const p=t.length-1,h=this.parts,[g,v]=Rt(t,e);if(this.el=K.createElement(g,i),T.currentNode=this.el.content,e===2||e===3){const u=this.el.content.firstChild;u.replaceWith(...u.childNodes)}for(;(r=T.nextNode())!==null&&h.length<p;){if(r.nodeType===1){if(r.hasAttributes())for(const u of r.getAttributeNames())if(u.endsWith(vt)){const C=v[n++],E=r.getAttribute(u).split(P),tt=/([.?@])?(.*)/.exec(C);h.push({type:1,index:s,name:tt[2],strings:E,ctor:tt[1]==="."?qt:tt[1]==="?"?Ft:tt[1]==="@"?Kt:Z}),r.removeAttribute(u)}else u.startsWith(P)&&(h.push({type:6,index:s}),r.removeAttribute(u));if(wt.test(r.tagName)){const u=r.textContent.split(P),C=u.length-1;if(C>0){r.textContent=Y?Y.emptyScript:"";for(let E=0;E<C;E++)r.append(u[E],N()),T.nextNode(),h.push({type:2,index:++s});r.append(u[C],N())}}}else if(r.nodeType===8)if(r.data===mt)h.push({type:2,index:s});else{let u=-1;for(;(u=r.data.indexOf(P,u+1))!==-1;)h.push({type:7,index:s}),u+=P.length-1}s++}}static createElement(t,e){const i=z.createElement("template");return i.innerHTML=t,i}}function L(o,t,e=o,i){var n,p;if(t===H)return t;let r=i!==void 0?(n=e._$Co)==null?void 0:n[i]:e._$Cl;const s=q(t)?void 0:t._$litDirective$;return(r==null?void 0:r.constructor)!==s&&((p=r==null?void 0:r._$AO)==null||p.call(r,!1),s===void 0?r=void 0:(r=new s(o),r._$AT(o,e,i)),i!==void 0?(e._$Co??(e._$Co=[]))[i]=r:e._$Cl=r),r!==void 0&&(t=L(o,r._$AS(o,t.values),r,i)),t}class Nt{constructor(t,e){this._$AV=[],this._$AN=void 0,this._$AD=t,this._$AM=e}get parentNode(){return this._$AM.parentNode}get _$AU(){return this._$AM._$AU}u(t){const{el:{content:e},parts:i}=this._$AD,r=((t==null?void 0:t.creationScope)??z).importNode(e,!0);T.currentNode=r;let s=T.nextNode(),n=0,p=0,h=i[0];for(;h!==void 0;){if(n===h.index){let g;h.type===2?g=new G(s,s.nextSibling,this,t):h.type===1?g=new h.ctor(s,h.name,h.strings,this,t):h.type===6&&(g=new Gt(s,this,t)),this._$AV.push(g),h=i[++p]}n!==(h==null?void 0:h.index)&&(s=T.nextNode(),n++)}return T.currentNode=z,r}p(t){let e=0;for(const i of this._$AV)i!==void 0&&(i.strings!==void 0?(i._$AI(t,i,e),e+=i.strings.length-2):i._$AI(t[e])),e++}}class G{get _$AU(){var t;return((t=this._$AM)==null?void 0:t._$AU)??this._$Cv}constructor(t,e,i,r){this.type=2,this._$AH=l,this._$AN=void 0,this._$AA=t,this._$AB=e,this._$AM=i,this.options=r,this._$Cv=(r==null?void 0:r.isConnected)??!0}get parentNode(){let t=this._$AA.parentNode;const e=this._$AM;return e!==void 0&&(t==null?void 0:t.nodeType)===11&&(t=e.parentNode),t}get startNode(){return this._$AA}get endNode(){return this._$AB}_$AI(t,e=this){t=L(this,t,e),q(t)?t===l||t==null||t===""?(this._$AH!==l&&this._$AR(),this._$AH=l):t!==this._$AH&&t!==H&&this._(t):t._$litType$!==void 0?this.$(t):t.nodeType!==void 0?this.T(t):Lt(t)?this.k(t):this._(t)}O(t){return this._$AA.parentNode.insertBefore(t,this._$AB)}T(t){this._$AH!==t&&(this._$AR(),this._$AH=this.O(t))}_(t){this._$AH!==l&&q(this._$AH)?this._$AA.nextSibling.data=t:this.T(z.createTextNode(t)),this._$AH=t}$(t){var s;const{values:e,_$litType$:i}=t,r=typeof i=="number"?this._$AC(t):(i.el===void 0&&(i.el=K.createElement(kt(i.h,i.h[0]),this.options)),i);if(((s=this._$AH)==null?void 0:s._$AD)===r)this._$AH.p(e);else{const n=new Nt(r,this),p=n.u(this.options);n.p(e),this.T(p),this._$AH=n}}_$AC(t){let e=Ot.get(t.strings);return e===void 0&&Ot.set(t.strings,e=new K(t)),e}k(t){st(this._$AH)||(this._$AH=[],this._$AR());const e=this._$AH;let i,r=0;for(const s of t)r===e.length?e.push(i=new G(this.O(N()),this.O(N()),this,this.options)):i=e[r],i._$AI(s),r++;r<e.length&&(this._$AR(i&&i._$AB.nextSibling,r),e.length=r)}_$AR(t=this._$AA.nextSibling,e){var i;for((i=this._$AP)==null?void 0:i.call(this,!1,!0,e);t!==this._$AB;){const r=gt(t).nextSibling;gt(t).remove(),t=r}}setConnected(t){var e;this._$AM===void 0&&(this._$Cv=t,(e=this._$AP)==null||e.call(this,t))}}class Z{get tagName(){return this.element.tagName}get _$AU(){return this._$AM._$AU}constructor(t,e,i,r,s){this.type=1,this._$AH=l,this._$AN=void 0,this.element=t,this.name=e,this._$AM=r,this.options=s,i.length>2||i[0]!==""||i[1]!==""?(this._$AH=Array(i.length-1).fill(new String),this.strings=i):this._$AH=l}_$AI(t,e=this,i,r){const s=this.strings;let n=!1;if(s===void 0)t=L(this,t,e,0),n=!q(t)||t!==this._$AH&&t!==H,n&&(this._$AH=t);else{const p=t;let h,g;for(t=s[0],h=0;h<s.length-1;h++)g=L(this,p[i+h],e,h),g===H&&(g=this._$AH[h]),n||(n=!q(g)||g!==this._$AH[h]),g===l?t=l:t!==l&&(t+=(g??"")+s[h+1]),this._$AH[h]=g}n&&!r&&this.j(t)}j(t){t===l?this.element.removeAttribute(this.name):this.element.setAttribute(this.name,t??"")}}class qt extends Z{constructor(){super(...arguments),this.type=3}j(t){this.element[this.name]=t===l?void 0:t}}class Ft extends Z{constructor(){super(...arguments),this.type=4}j(t){this.element.toggleAttribute(this.name,!!t&&t!==l)}}class Kt extends Z{constructor(t,e,i,r,s){super(t,e,i,r,s),this.type=5}_$AI(t,e=this){if((t=L(this,t,e,0)??l)===H)return;const i=this._$AH,r=t===l&&i!==l||t.capture!==i.capture||t.once!==i.once||t.passive!==i.passive,s=t!==l&&(i===l||r);r&&this.element.removeEventListener(this.name,this,i),s&&this.element.addEventListener(this.name,this,t),this._$AH=t}handleEvent(t){var e;typeof this._$AH=="function"?this._$AH.call(((e=this.options)==null?void 0:e.host)??this.element,t):this._$AH.handleEvent(t)}}class Gt{constructor(t,e,i){this.element=t,this.type=6,this._$AN=void 0,this._$AM=e,this.options=i}get _$AU(){return this._$AM._$AU}_$AI(t){L(this,t)}}const nt=R.litHtmlPolyfillSupport;nt==null||nt(K,G),(R.litHtmlVersions??(R.litHtmlVersions=[])).push("3.3.3");const Wt=(o,t,e)=>{const i=(e==null?void 0:e.renderBefore)??t;let r=i._$litPart$;if(r===void 0){const s=(e==null?void 0:e.renderBefore)??null;i._$litPart$=r=new G(t.insertBefore(N(),s),s,void 0,e??{})}return r._$AI(o),r};/**
 * @license
 * Copyright 2017 Google LLC
 * SPDX-License-Identifier: BSD-3-Clause
 */const M=globalThis;class y extends D{constructor(){super(...arguments),this.renderOptions={host:this},this._$Do=void 0}createRenderRoot(){var e;const t=super.createRenderRoot();return(e=this.renderOptions).renderBefore??(e.renderBefore=t.firstChild),t}update(t){const e=this.render();this.hasUpdated||(this.renderOptions.isConnected=this.isConnected),super.update(t),this._$Do=Wt(e,this.renderRoot,this.renderOptions)}connectedCallback(){var t;super.connectedCallback(),(t=this._$Do)==null||t.setConnected(!0)}disconnectedCallback(){var t;super.disconnectedCallback(),(t=this._$Do)==null||t.setConnected(!1)}render(){return H}}y._$litElement$=!0,y.finalized=!0,(St=M.litElementHydrateSupport)==null||St.call(M,{LitElement:y});const ct=M.litElementPolyfillSupport;ct==null||ct({LitElement:y}),(M.litElementVersions??(M.litElementVersions=[])).push("4.2.2");/**
 * @license
 * Copyright 2017 Google LLC
 * SPDX-License-Identifier: BSD-3-Clause
 */const w=o=>(t,e)=>{e!==void 0?e.addInitializer(()=>{customElements.define(o,t)}):customElements.define(o,t)};/**
 * @license
 * Copyright 2017 Google LLC
 * SPDX-License-Identifier: BSD-3-Clause
 */const Vt={attribute:!0,type:String,converter:J,reflect:!1,hasChanged:ot},Xt=(o=Vt,t,e)=>{const{kind:i,metadata:r}=e;let s=globalThis.litPropertyMetadata.get(r);if(s===void 0&&globalThis.litPropertyMetadata.set(r,s=new Map),i==="setter"&&((o=Object.create(o)).wrapped=!0),s.set(e.name,o),i==="accessor"){const{name:n}=e;return{set(p){const h=t.get.call(this);t.set.call(this,p),this.requestUpdate(n,h,o,!0,p)},init(p){return p!==void 0&&this.C(n,void 0,o,p),p}}}if(i==="setter"){const{name:n}=e;return function(p){const h=this[n];t.call(this,p),this.requestUpdate(n,h,o,!0,p)}}throw Error("Unsupported decorator location: "+i)};function d(o){return(t,e)=>typeof e=="object"?Xt(o,t,e):((i,r,s)=>{const n=r.hasOwnProperty(s);return r.constructor.createProperty(s,i),n?Object.getOwnPropertyDescriptor(r,s):void 0})(o,t,e)}/**
 * @license
 * Copyright 2017 Google LLC
 * SPDX-License-Identifier: BSD-3-Clause
 */function f(o){return d({...o,state:!0,attribute:!1})}/**
 * @license
 * Copyright 2017 Google LLC
 * SPDX-License-Identifier: BSD-3-Clause
 */const Jt=(o,t,e)=>(e.configurable=!0,e.enumerable=!0,Reflect.decorate&&typeof t!="object"&&Object.defineProperty(o,t,e),e);/**
 * @license
 * Copyright 2017 Google LLC
 * SPDX-License-Identifier: BSD-3-Clause
 */function Yt(o,t){return(e,i,r)=>{const s=n=>{var p;return((p=n.renderRoot)==null?void 0:p.querySelector(o))??null};return Jt(e,i,{get(){return s(this)}})}}function Zt(){return typeof window<"u"&&window.OD_ANALYTICS_ENDPOINT||"/api/v1/events"}function Q(o){if(!o.publicationKey)return;const t={events:[{eventType:o.eventType,publicationKey:o.publicationKey,offerId:o.offerId,channel:o.channel??"WebComponent",occurredAt:o.occurredAt??new Date().toISOString()}]},e=Zt(),i=JSON.stringify(t);try{if(typeof navigator<"u"&&typeof navigator.sendBeacon=="function"){navigator.sendBeacon(e,new Blob([i],{type:"application/json"}));return}}catch{}typeof fetch=="function"&&fetch(e,{method:"POST",headers:{"Content-Type":"application/json"},body:i,keepalive:!0}).catch(()=>{})}var Qt=Object.defineProperty,te=Object.getOwnPropertyDescriptor,$=(o,t,e,i)=>{for(var r=i>1?void 0:i?te(t,e):t,s=o.length-1,n;s>=0;s--)(n=o[s])&&(r=(i?n(t,e,r):n(r))||r);return i&&r&&Qt(t,e,r),r};a.OdOfferCard=class extends y{constructor(){super(...arguments),this.offer=null,this.offerId="",this.apiBase="",this.ctaLabel="Виж офертата",this.cardStyle="default",this.publicationKey="",this.channel="WebComponent",this._loading=!1,this._error="",this._impressionSent=!1}connectedCallback(){super.connectedCallback(),!this.offer&&this.offerId&&this._fetch(),this._observeImpression()}disconnectedCallback(){var t;super.disconnectedCallback(),(t=this._io)==null||t.disconnect(),this._io=void 0}updated(t){(t.has("offerId")||t.has("apiBase"))&&!this.offer&&this.offerId&&this._fetch()}_observeImpression(){this._impressionSent||typeof IntersectionObserver>"u"||(this._io=new IntersectionObserver(t=>{var e;for(const i of t)if(i.isIntersecting){this._emit("Impression","od-impression"),this._impressionSent=!0,(e=this._io)==null||e.disconnect(),this._io=void 0;break}}),this._io.observe(this))}_emit(t,e){var r;const i=(((r=this.offer)==null?void 0:r.id)??this.offerId)||void 0;this.dispatchEvent(new CustomEvent(e,{detail:{offer:this.offer,offerId:i,publicationKey:this.publicationKey},bubbles:!0,composed:!0})),Q({eventType:t,publicationKey:this.publicationKey,offerId:i,channel:this.channel})}async _fetch(){const t=`${this.apiBase}/api/v1/offers/${this.offerId}`;this._loading=!0,this._error="";try{const e=await fetch(t);if(!e.ok)throw new Error(`HTTP ${e.status}`);this.offer=await e.json()}catch(e){this._error=e.message}finally{this._loading=!1}}_onCta(t){t.stopPropagation(),this._emit("Open","od-offer-open"),this.dispatchEvent(new CustomEvent("od-cta-click",{detail:{offer:this.offer},bubbles:!0,composed:!0}))}_nights(t){return t.nights??t.durationNights??0}_img(t){return t.img??t.imageUrl??""}_board(t){return A[t.board??t.boardBasis??""]??""}_cat(t){return V[t.cat??""]??""}_loc(t){const e=t.region??t.city??"";return e?`${e}, ${t.country}`:t.country}_pin(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>`}_moon(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>`}_plane(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 16v-2l-8-5V3.5a1.5 1.5 0 0 0-3 0V9l-8 5v2l8-2.5V19l-2 1.5V22l3.5-1 3.5 1v-1.5L13 19v-5.5z"/></svg>`}_bus(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="1" y="3" width="15" height="13" rx="2"/><path d="M16 8h4l3 3v5h-7V8z"/><circle cx="5.5" cy="18.5" r="2.5"/><circle cx="18.5" cy="18.5" r="2.5"/></svg>`}_heart(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>`}_arrowR(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`}render(){if(this._loading)return c`<div class="status">Зарежда…</div>`;if(this._error)return c`<div class="status error">${this._error}</div>`;if(!this.offer)return l;const t=this.offer,e=this._nights(t),i=this._img(t),r=this._board(t),s=this._cat(t),n=this._loc(t);return c`
      <article class="card" part="card">
        <div class="media">
          ${i?c`<img part="image" src=${i} alt=${t.title} loading="lazy" />`:l}
          ${s?c`<span class="tag" part="tag">${s}</span>`:l}
          <button class="fav" aria-label="Запази в любими">${this._heart()}</button>
        </div>

        <div class="body" part="body">
          ${n?c`<span class="loc">${this._pin()}${n}</span>`:l}

          <h3 class="title" part="title">${t.title}</h3>

          <div class="meta" part="meta">
            ${e?c`<span class="chip">${this._moon()}${e} нощувки</span>`:l}
            ${r?c`<span class="chip">${r}</span>`:l}
            ${t.transport?c`
              <span class="chip">
                ${t.transport==="plane"?this._plane():this._bus()}
                ${t.transport==="plane"?"Самолет":"Автобус"}
              </span>`:l}
          </div>

          <div class="foot">
            <div class="price-wrap">
              <span class="from">от</span>
              <span class="price" part="price">€${t.price}<small> / човек</small></span>
            </div>
            <button class="cta" part="cta" @click=${this._onCta}>
              ${this.ctaLabel}${this._arrowR()}
            </button>
          </div>

          <slot name="actions"></slot>
        </div>
      </article>
    `}},a.OdOfferCard.styles=x`
    :host {
      display: block;
      --odc-font:         system-ui, sans-serif;
      --odc-font-head:    system-ui, sans-serif;
      --odc-accent:       #1a5a61;
      --odc-accent-ink:   #ffffff;
      --odc-accent-soft:  #eef6f6;
      --odc-price:        #0e1618;
      --odc-ink:          #15201f;
      --odc-muted:        #5f6b68;
      --odc-bg:           #ffffff;
      --odc-surface:      #ffffff;
      --odc-border:       rgba(20,30,28,0.12);
      --odc-radius:       14px;
      --odc-radius-sm:    9px;
      --odc-shadow:       0 1px 2px rgba(16,24,22,0.06);
      --odc-shadow-hover: 0 12px 30px rgba(16,24,22,0.14);
      --odc-tag-bg:       #15201f;
      --odc-tag-ink:      #ffffff;
    }
    *, *::before, *::after { box-sizing: border-box; }

    /* ── base card ── */
    .card {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius);
      overflow: hidden;
      box-shadow: var(--odc-shadow);
      transition: transform 0.2s ease, box-shadow 0.2s ease;
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
    }
    .card:hover {
      transform: translateY(-3px);
      box-shadow: var(--odc-shadow-hover);
    }

    /* ── media ── */
    .media {
      position: relative;
      aspect-ratio: 4 / 3;
      overflow: hidden;
      background: var(--odc-accent-soft);
      flex: none;
    }
    .media img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      transition: transform 0.6s ease;
      display: block;
    }
    .card:hover .media img { transform: scale(1.04); }

    .tag {
      position: absolute;
      top: 12px;
      left: 12px;
      background: var(--odc-tag-bg);
      color: var(--odc-tag-ink);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      padding: 5px 10px;
      border-radius: 999px;
      pointer-events: none;
    }
    .fav {
      position: absolute;
      top: 10px;
      right: 10px;
      width: 34px;
      height: 34px;
      display: grid;
      place-items: center;
      background: rgba(255,255,255,0.9);
      border-radius: 999px;
      color: var(--odc-ink);
      border: 0;
      cursor: pointer;
      backdrop-filter: blur(4px);
      padding: 0;
    }
    .fav svg { width: 17px; height: 17px; }

    /* ── body ── */
    .body {
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      flex: 1;
    }
    .loc {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: 12.5px;
      color: var(--odc-muted);
      font-weight: 500;
    }
    .loc svg { width: 14px; height: 14px; flex: none; }
    .title {
      font-family: var(--odc-font-head);
      font-size: 18px;
      font-weight: 600;
      line-height: 1.25;
      color: var(--odc-ink);
      margin: 0;
    }
    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      font-size: 12px;
      color: var(--odc-muted);
      background: var(--odc-accent-soft);
      border-radius: 999px;
      padding: 4px 10px;
      white-space: nowrap;
    }
    .chip svg { width: 13px; height: 13px; flex: none; }

    /* ── footer ── */
    .foot {
      margin-top: auto;
      padding-top: 12px;
      border-top: 1px solid var(--odc-border);
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 10px;
    }
    .price-wrap { display: flex; flex-direction: column; gap: 1px; }
    .from { font-size: 11px; color: var(--odc-muted); }
    .price {
      font-family: var(--odc-font-head);
      font-size: 22px;
      font-weight: 700;
      color: var(--odc-price);
      line-height: 1.1;
    }
    .price small { font-size: 12px; font-weight: 500; color: var(--odc-muted); }
    .cta {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border: 0;
      border-radius: var(--odc-radius-sm);
      padding: 9px 14px;
      font-family: var(--odc-font);
      font-size: 13.5px;
      font-weight: 600;
      cursor: pointer;
      transition: filter 0.15s ease;
      white-space: nowrap;
      flex: none;
    }
    .cta:hover { filter: brightness(1.06); }
    .cta svg { width: 15px; height: 15px; flex: none; }

    /* ── compact variant ── */
    :host([card-style="compact"]) .media { aspect-ratio: 16 / 9; }
    :host([card-style="compact"]) .body { padding: 13px; gap: 7px; }
    :host([card-style="compact"]) .title { font-size: 15.5px; }
    :host([card-style="compact"]) .chip { background: transparent; padding: 0; gap: 4px; }
    :host([card-style="compact"]) .price { font-size: 19px; }

    /* ── editorial variant ── */
    :host([card-style="editorial"]) .card {
      border: none;
      box-shadow: none;
      background: transparent;
    }
    :host([card-style="editorial"]) .media { aspect-ratio: 3 / 4; border-radius: var(--odc-radius); }
    :host([card-style="editorial"]) .card:hover { transform: none; }
    :host([card-style="editorial"]) .card:hover .media img { transform: scale(1.05); }
    :host([card-style="editorial"]) .tag {
      top: auto;
      bottom: 12px;
      left: 12px;
      background: rgba(255,255,255,0.92);
      color: var(--odc-ink);
    }
    :host([card-style="editorial"]) .body { padding: 13px 2px 4px; gap: 6px; }
    :host([card-style="editorial"]) .title { font-size: 17px; }
    :host([card-style="editorial"]) .meta { display: none; }
    :host([card-style="editorial"]) .foot { border-top: none; padding-top: 6px; align-items: center; }
    :host([card-style="editorial"]) .from { display: none; }
    :host([card-style="editorial"]) .price { font-size: 17px; }
    :host([card-style="editorial"]) .cta {
      background: transparent;
      color: var(--odc-accent);
      padding: 6px 0;
    }

    /* ── status ── */
    .status {
      padding: 20px;
      text-align: center;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 14px;
    }
    .status.error { color: #b3261e; }
  `,$([d({type:Object})],a.OdOfferCard.prototype,"offer",2),$([d({attribute:"offer-id"})],a.OdOfferCard.prototype,"offerId",2),$([d({attribute:"api-base"})],a.OdOfferCard.prototype,"apiBase",2),$([d({attribute:"cta-label"})],a.OdOfferCard.prototype,"ctaLabel",2),$([d({attribute:"card-style",reflect:!0})],a.OdOfferCard.prototype,"cardStyle",2),$([d({attribute:"publication-key"})],a.OdOfferCard.prototype,"publicationKey",2),$([d({attribute:"channel"})],a.OdOfferCard.prototype,"channel",2),$([f()],a.OdOfferCard.prototype,"_loading",2),$([f()],a.OdOfferCard.prototype,"_error",2),a.OdOfferCard=$([w("od-offer-card")],a.OdOfferCard);function dt(o){return o?{Authorization:`ApiKey ${o}`}:{}}async function Ct(o,t,e){if(t.endpoint)return t.endpoint;if(t.publication){const i=`${o}/api/v1/publications/${encodeURIComponent(t.publication)}`,r=await fetch(i,{headers:dt(e)});if(!r.ok)throw new Error(`manifest HTTP ${r.status}`);const s=await r.json();return`${o}${s.offersUrl}`}return t.collection?`${o}/api/v1/collections/${encodeURIComponent(t.collection)}/offers`:""}function At(o){return!!(o.endpoint||o.publication||o.collection)}var ee=Object.defineProperty,ie=Object.getOwnPropertyDescriptor,m=(o,t,e,i)=>{for(var r=i>1?void 0:i?ie(t,e):t,s=o.length-1,n;s>=0;s--)(n=o[s])&&(r=(i?n(t,e,r):n(r))||r);return i&&r&&ee(t,e,r),r};a.OdOfferGrid=class extends y{constructor(){super(...arguments),this.apiBase="",this.apiKey="",this.cardStyle="default",this.ctaLabel="Виж офертата",this._fetched=[],this._loading=!1,this._error=""}get _hasSource(){return At({endpoint:this.endpoint,publication:this.publication,collection:this.collection})}connectedCallback(){super.connectedCallback(),!this.offers&&this._hasSource&&this._load()}updated(t){["collection","publication","endpoint","apiBase","apiKey"].some(i=>t.has(i))&&!this.offers&&this._load()}async _load(){if(this._hasSource){this._loading=!0,this._error="";try{const t=await Ct(this.apiBase,{endpoint:this.endpoint,publication:this.publication,collection:this.collection},this.apiKey);if(!t)return;const e=await fetch(t,{headers:dt(this.apiKey)});if(!e.ok)throw new Error(`HTTP ${e.status}`);this._fetched=await e.json()}catch(t){this._error=t.message}finally{this._loading=!1}}}_gridStyle(){return this.columns?`grid-template-columns: repeat(${this.columns}, minmax(0, 1fr))`:""}render(){const t=this.offers??this._fetched;if(this._loading&&t.length===0){const e=this.columns??3;return c`
        ${this.title?c`<div class="head" part="head"><h2 class="head-title">${this.title}</h2></div>`:l}
        <div class="grid" style=${this._gridStyle()} part="loading">
          ${Array.from({length:e},()=>c`<div class="skel"></div>`)}
        </div>
      `}return this._error?c`<div class="status error" part="error">Грешка при зареждане: ${this._error}</div>`:t.length===0?c`<div class="status" part="empty">Няма намерени оферти.</div>`:c`
      ${this.title?c`
        <div class="head" part="head">
          <h2 class="head-title">${this.title}</h2>
          <span class="count">${t.length} оферти</span>
        </div>`:l}
      <div class="grid" style=${this._gridStyle()} part="grid">
        ${t.map(e=>c`
          <od-offer-card
            .offer=${e}
            cta-label=${this.ctaLabel}
            card-style=${this.cardStyle}
          ></od-offer-card>
        `)}
      </div>
    `}},a.OdOfferGrid.styles=x`
    :host {
      display: block;
      --odc-font:        system-ui, sans-serif;
      --odc-font-head:   system-ui, sans-serif;
      --odc-accent:      #1a5a61;
      --odc-accent-soft: #eef6f6;
      --odc-ink:         #15201f;
      --odc-muted:       #5f6b68;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
      --odc-radius-sm:   9px;
    }
    *, *::before, *::after { box-sizing: border-box; }

    .head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      margin-bottom: 14px;
      gap: 12px;
      font-family: var(--odc-font);
    }
    .head-title {
      font-family: var(--odc-font-head);
      font-size: 22px;
      font-weight: 700;
      margin: 0;
      color: var(--odc-ink);
    }
    .count {
      font-size: 13px;
      color: var(--odc-muted);
      white-space: nowrap;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 18px;
    }
    .status {
      padding: 32px;
      text-align: center;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 14px;
    }
    .status.error { color: #b3261e; }
    .skel {
      background: var(--odc-accent-soft);
      border-radius: var(--odc-radius);
      height: 340px;
      animation: pulse 1.4s ease-in-out infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50%       { opacity: 0.4; }
    }
  `,m([d({type:String})],a.OdOfferGrid.prototype,"collection",2),m([d({type:String})],a.OdOfferGrid.prototype,"publication",2),m([d({type:String})],a.OdOfferGrid.prototype,"endpoint",2),m([d({attribute:"api-base"})],a.OdOfferGrid.prototype,"apiBase",2),m([d({attribute:"api-key"})],a.OdOfferGrid.prototype,"apiKey",2),m([d({type:Number})],a.OdOfferGrid.prototype,"columns",2),m([d({attribute:"card-style"})],a.OdOfferGrid.prototype,"cardStyle",2),m([d({type:String})],a.OdOfferGrid.prototype,"title",2),m([d({attribute:"cta-label"})],a.OdOfferGrid.prototype,"ctaLabel",2),m([d({type:Array})],a.OdOfferGrid.prototype,"offers",2),m([f()],a.OdOfferGrid.prototype,"_fetched",2),m([f()],a.OdOfferGrid.prototype,"_loading",2),m([f()],a.OdOfferGrid.prototype,"_error",2),a.OdOfferGrid=m([w("od-offer-grid")],a.OdOfferGrid);var re=Object.defineProperty,oe=Object.getOwnPropertyDescriptor,b=(o,t,e,i)=>{for(var r=i>1?void 0:i?oe(t,e):t,s=o.length-1,n;s>=0;s--)(n=o[s])&&(r=(i?n(t,e,r):n(r))||r);return i&&r&&re(t,e,r),r};a.OdOfferCarousel=class extends y{constructor(){super(...arguments),this.apiBase="",this.apiKey="",this.cardStyle="default",this.ctaLabel="Виж офертата",this.cardWidth=248,this._fetched=[],this._loading=!1,this._error="",this._canPrev=!1,this._canNext=!0,this._touchStartX=0}get _hasSource(){return At({endpoint:this.endpoint,publication:this.publication,collection:this.collection})}connectedCallback(){super.connectedCallback(),!this.offers&&this._hasSource&&this._load()}updated(t){["collection","publication","endpoint","apiBase","apiKey"].some(i=>t.has(i))&&!this.offers&&this._load(),t.has("cardWidth")&&this.style.setProperty("--_card-w",`${this.cardWidth}px`)}async _load(){if(this._hasSource){this._loading=!0,this._error="";try{const t=await Ct(this.apiBase,{endpoint:this.endpoint,publication:this.publication,collection:this.collection},this.apiKey);if(!t)return;const e=await fetch(t,{headers:dt(this.apiKey)});if(!e.ok)throw new Error(`HTTP ${e.status}`);this._fetched=await e.json()}catch(t){this._error=t.message}finally{this._loading=!1}}}_onScroll(){const t=this._track;t&&(this._canPrev=t.scrollLeft>8,this._canNext=t.scrollLeft+t.clientWidth<t.scrollWidth-8)}_scroll(t){const e=this._track;e&&e.scrollBy({left:t*(this.cardWidth+16),behavior:"smooth"})}_onTouchStart(t){this._touchStartX=t.touches[0].clientX}_onTouchEnd(t){const e=t.changedTouches[0].clientX-this._touchStartX;Math.abs(e)>40&&this._scroll(e<0?1:-1)}_onKeyDown(t){t.key==="ArrowLeft"&&(t.preventDefault(),this._scroll(-1)),t.key==="ArrowRight"&&(t.preventDefault(),this._scroll(1))}_chevL(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 18l-6-6 6-6"/></svg>`}_chevR(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 18l6-6-6-6"/></svg>`}render(){const t=this.offers??this._fetched,e=this.title?c`
      <div class="head" part="head">
        <h2 class="head-title">${this.title}</h2>
        <div class="nav-row">
          <button
            class="nav-btn"
            part="nav-prev"
            aria-label="Предишни"
            ?disabled=${!this._canPrev}
            @click=${()=>this._scroll(-1)}
          >${this._chevL()}</button>
          <button
            class="nav-btn"
            part="nav-next"
            aria-label="Следващи"
            ?disabled=${!this._canNext}
            @click=${()=>this._scroll(1)}
          >${this._chevR()}</button>
        </div>
      </div>`:l;return this._loading&&t.length===0?c`
        ${e}
        <div class="wrap">
          <div class="track" part="loading">
            ${Array.from({length:4},()=>c`<div class="skel"></div>`)}
          </div>
        </div>
      `:this._error?c`<div class="status error" part="error">Грешка при зареждане: ${this._error}</div>`:t.length===0?c`<div class="status" part="empty">Няма намерени оферти.</div>`:c`
      ${e}
      <div
        class="wrap"
        @touchstart=${this._onTouchStart}
        @touchend=${this._onTouchEnd}
        @keydown=${this._onKeyDown}
        tabindex="0"
        role="region"
        aria-label=${this.title??"Оферти"}
      >
        <div class="track" part="track" @scroll=${this._onScroll}>
          ${t.map(i=>c`
            <od-offer-card
              .offer=${i}
              cta-label=${this.ctaLabel}
              card-style=${this.cardStyle}
            ></od-offer-card>
          `)}
        </div>
      </div>
    `}},a.OdOfferCarousel.styles=x`
    :host {
      display: block;
      --odc-font:        system-ui, sans-serif;
      --odc-font-head:   system-ui, sans-serif;
      --odc-accent:      #1a5a61;
      --odc-accent-ink:  #ffffff;
      --odc-accent-soft: #eef6f6;
      --odc-ink:         #15201f;
      --odc-muted:       #5f6b68;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
      --odc-shadow:      0 1px 2px rgba(16,24,22,0.06);
    }
    *, *::before, *::after { box-sizing: border-box; }

    .head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 14px;
      gap: 12px;
      font-family: var(--odc-font);
    }
    .head-title {
      font-family: var(--odc-font-head);
      font-size: 22px;
      font-weight: 700;
      margin: 0;
      color: var(--odc-ink);
    }
    .nav-row {
      display: flex;
      gap: 8px;
      flex: none;
    }
    .nav-btn {
      width: 38px;
      height: 38px;
      border-radius: 999px;
      border: 1px solid var(--odc-border);
      background: var(--odc-surface);
      color: var(--odc-ink);
      display: grid;
      place-items: center;
      cursor: pointer;
      box-shadow: var(--odc-shadow);
      transition: background 0.15s ease, color 0.15s ease;
      padding: 0;
    }
    .nav-btn:hover {
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border-color: var(--odc-accent);
    }
    .nav-btn:disabled { opacity: 0.35; cursor: not-allowed; }
    .nav-btn:disabled:hover { background: var(--odc-surface); color: var(--odc-ink); border-color: var(--odc-border); }
    .nav-btn svg { width: 18px; height: 18px; }

    .wrap { position: relative; overflow: hidden; }
    .track {
      display: flex;
      gap: 16px;
      overflow-x: auto;
      scroll-snap-type: x mandatory;
      scroll-behavior: smooth;
      padding: 4px 2px 14px;
      scrollbar-width: thin;
      scrollbar-color: var(--odc-border) transparent;
      -webkit-overflow-scrolling: touch;
    }
    .track::-webkit-scrollbar { height: 6px; }
    .track::-webkit-scrollbar-thumb { background: var(--odc-border); border-radius: 999px; }
    .track > od-offer-card {
      scroll-snap-align: start;
      flex: 0 0 var(--_card-w, 248px);
    }

    .status {
      padding: 32px;
      text-align: center;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 14px;
    }
    .status.error { color: #b3261e; }
    .skel {
      flex: 0 0 248px;
      height: 340px;
      background: var(--odc-accent-soft);
      border-radius: var(--odc-radius);
      animation: pulse 1.4s ease-in-out infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50%       { opacity: 0.4; }
    }
  `,b([d({type:String})],a.OdOfferCarousel.prototype,"collection",2),b([d({type:String})],a.OdOfferCarousel.prototype,"publication",2),b([d({type:String})],a.OdOfferCarousel.prototype,"endpoint",2),b([d({attribute:"api-base"})],a.OdOfferCarousel.prototype,"apiBase",2),b([d({attribute:"api-key"})],a.OdOfferCarousel.prototype,"apiKey",2),b([d({attribute:"card-style"})],a.OdOfferCarousel.prototype,"cardStyle",2),b([d({type:String})],a.OdOfferCarousel.prototype,"title",2),b([d({attribute:"cta-label"})],a.OdOfferCarousel.prototype,"ctaLabel",2),b([d({attribute:"card-width",type:Number})],a.OdOfferCarousel.prototype,"cardWidth",2),b([d({type:Array})],a.OdOfferCarousel.prototype,"offers",2),b([f()],a.OdOfferCarousel.prototype,"_fetched",2),b([f()],a.OdOfferCarousel.prototype,"_loading",2),b([f()],a.OdOfferCarousel.prototype,"_error",2),b([f()],a.OdOfferCarousel.prototype,"_canPrev",2),b([f()],a.OdOfferCarousel.prototype,"_canNext",2),b([Yt(".track")],a.OdOfferCarousel.prototype,"_track",2),a.OdOfferCarousel=b([w("od-offer-carousel")],a.OdOfferCarousel);var se=Object.defineProperty,ae=Object.getOwnPropertyDescriptor,O=(o,t,e,i)=>{for(var r=i>1?void 0:i?ae(t,e):t,s=o.length-1,n;s>=0;s--)(n=o[s])&&(r=(i?n(t,e,r):n(r))||r);return i&&r&&se(t,e,r),r};a.OdFilterPanel=class extends y{constructor(){super(...arguments),this.title="Филтри",this.maxPrice=3e3,this.defaultMaxPrice=3e3,this.offers=[],this._countries=new Set,this._boards=new Set,this._cats=new Set,this._priceMax=3e3}connectedCallback(){super.connectedCallback(),this._priceMax=this.defaultMaxPrice||this.maxPrice}updated(t){(t.has("defaultMaxPrice")||t.has("maxPrice"))&&(this._priceMax=this.defaultMaxPrice||this.maxPrice)}_emit(){this.dispatchEvent(new CustomEvent("od-filter-change",{detail:{countries:[...this._countries],boards:[...this._boards],cats:[...this._cats],maxPrice:this._priceMax},bubbles:!0,composed:!0}))}_toggleCountry(t){const e=new Set(this._countries);e.has(t)?e.delete(t):e.add(t),this._countries=e,this._emit()}_toggleBoard(t){const e=new Set(this._boards);e.has(t)?e.delete(t):e.add(t),this._boards=e,this._emit()}_toggleCat(t){const e=new Set(this._cats);e.has(t)?e.delete(t):e.add(t),this._cats=e,this._emit()}_onPrice(t){this._priceMax=Number(t.target.value),this._emit()}_clear(){this._countries=new Set,this._boards=new Set,this._cats=new Set,this._priceMax=this.defaultMaxPrice||this.maxPrice,this._emit()}_facetCountries(){const t=new Map;for(const e of this.offers)t.set(e.country,(t.get(e.country)??0)+1);return[...t.entries()].sort((e,i)=>i[1]-e[1])}_facetBoards(){const t=new Map;for(const e of this.offers){const i=e.board??e.boardBasis??"";i&&t.set(i,(t.get(i)??0)+1)}return[...t.entries()].sort((e,i)=>i[1]-e[1])}_facetCats(){const t=new Map;for(const e of this.offers){const i=e.cat??"";i&&t.set(i,(t.get(i)??0)+1)}return[...t.entries()].sort((e,i)=>i[1]-e[1])}render(){const t=this._facetCountries(),e=this._facetBoards(),i=this._facetCats(),r=this._countries.size>0||this._boards.size>0||this._cats.size>0||this._priceMax<this.maxPrice;return c`
      <aside class="panel" part="panel">
        <h4 part="heading">${this.title}</h4>

        ${t.length?c`
          <div class="group" part="group">
            <div class="group-label">Дестинация</div>
            ${t.map(([s,n])=>c`
              <label class="check">
                <input
                  type="checkbox"
                  .checked=${this._countries.has(s)}
                  @change=${()=>this._toggleCountry(s)}
                />
                ${s}
                <span class="count">${n}</span>
              </label>
            `)}
          </div>`:l}

        ${i.length?c`
          <div class="group" part="group">
            <div class="group-label">Тип почивка</div>
            ${i.map(([s,n])=>c`
              <label class="check">
                <input
                  type="checkbox"
                  .checked=${this._cats.has(s)}
                  @change=${()=>this._toggleCat(s)}
                />
                ${V[s]??s}
                <span class="count">${n}</span>
              </label>
            `)}
          </div>`:l}

        ${e.length?c`
          <div class="group" part="group">
            <div class="group-label">Изхранване</div>
            ${e.map(([s,n])=>c`
              <label class="check">
                <input
                  type="checkbox"
                  .checked=${this._boards.has(s)}
                  @change=${()=>this._toggleBoard(s)}
                />
                ${A[s]??s}
                <span class="count">${n}</span>
              </label>
            `)}
          </div>`:l}

        <div class="group" part="group">
          <div class="group-label">Бюджет</div>
          <div class="range-wrap">
            <span class="range-value">до €${this._priceMax}</span>
            <input
              type="range"
              class="range-track"
              min="100"
              .max=${String(this.maxPrice)}
              .value=${String(this._priceMax)}
              @input=${this._onPrice}
            />
            <div class="range-labels">
              <span>€100</span>
              <span>€${this.maxPrice}</span>
            </div>
          </div>
        </div>

        ${r?c`
          <button class="clear-btn" @click=${this._clear}>Изчисти филтрите</button>
        `:l}
      </aside>
    `}},a.OdFilterPanel.styles=x`
    :host {
      display: block;
      --odc-font:        system-ui, sans-serif;
      --odc-font-head:   system-ui, sans-serif;
      --odc-accent:      #1a5a61;
      --odc-accent-soft: #eef6f6;
      --odc-ink:         #15201f;
      --odc-muted:       #5f6b68;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
      --odc-radius-sm:   9px;
    }
    *, *::before, *::after { box-sizing: border-box; }

    .panel {
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius);
      padding: 16px;
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
      align-self: start;
    }
    h4 {
      font-family: var(--odc-font-head);
      font-size: 15px;
      font-weight: 700;
      margin: 0 0 12px;
      color: var(--odc-ink);
    }
    .group {
      padding: 12px 0;
      border-top: 1px solid var(--odc-border);
    }
    .group:first-of-type { border-top: none; padding-top: 0; }
    .group-label {
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.03em;
      text-transform: uppercase;
      color: var(--odc-muted);
      margin-bottom: 9px;
    }
    .check {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 4px 0;
      font-size: 13.5px;
      color: var(--odc-ink);
      cursor: pointer;
      user-select: none;
    }
    .check input {
      width: 15px;
      height: 15px;
      accent-color: var(--odc-accent);
      cursor: pointer;
      flex: none;
    }
    .count {
      margin-left: auto;
      font-size: 12px;
      color: var(--odc-muted);
    }

    /* price range */
    .range-wrap { display: flex; flex-direction: column; gap: 8px; }
    .range-track {
      -webkit-appearance: none;
      appearance: none;
      width: 100%;
      height: 4px;
      border-radius: 999px;
      background: var(--odc-border);
      accent-color: var(--odc-accent);
      cursor: pointer;
    }
    .range-track::-webkit-slider-thumb {
      -webkit-appearance: none;
      width: 18px;
      height: 18px;
      border-radius: 999px;
      background: var(--odc-accent);
      cursor: pointer;
      box-shadow: 0 1px 4px rgba(0,0,0,0.15);
    }
    .range-labels {
      display: flex;
      justify-content: space-between;
      font-size: 12px;
      color: var(--odc-muted);
    }
    .range-value {
      font-size: 13px;
      font-weight: 600;
      color: var(--odc-ink);
      text-align: right;
    }

    .clear-btn {
      margin-top: 14px;
      width: 100%;
      height: 36px;
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius-sm);
      background: transparent;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 13px;
      cursor: pointer;
      transition: background 0.15s ease, color 0.15s ease;
    }
    .clear-btn:hover { background: var(--odc-accent-soft); color: var(--odc-ink); }
  `,O([d({type:String})],a.OdFilterPanel.prototype,"title",2),O([d({attribute:"max-price",type:Number})],a.OdFilterPanel.prototype,"maxPrice",2),O([d({attribute:"default-max-price",type:Number})],a.OdFilterPanel.prototype,"defaultMaxPrice",2),O([d({type:Array})],a.OdFilterPanel.prototype,"offers",2),O([f()],a.OdFilterPanel.prototype,"_countries",2),O([f()],a.OdFilterPanel.prototype,"_boards",2),O([f()],a.OdFilterPanel.prototype,"_cats",2),O([f()],a.OdFilterPanel.prototype,"_priceMax",2),a.OdFilterPanel=O([w("od-filter-panel")],a.OdFilterPanel);var ne=Object.defineProperty,ce=Object.getOwnPropertyDescriptor,j=(o,t,e,i)=>{for(var r=i>1?void 0:i?ce(t,e):t,s=o.length-1,n;s>=0;s--)(n=o[s])&&(r=(i?n(t,e,r):n(r))||r);return i&&r&&ne(t,e,r),r};a.OdOfferDetails=class extends y{constructor(){super(...arguments),this.offer=null,this.offerId="",this.apiBase="",this.ctaLabel="Запитване",this._loading=!1,this._error=""}connectedCallback(){super.connectedCallback(),!this.offer&&this.offerId&&this._fetch()}updated(t){(t.has("offerId")||t.has("apiBase"))&&!this.offer&&this.offerId&&this._fetch()}async _fetch(){const t=`${this.apiBase}/api/v1/offers/${this.offerId}`;this._loading=!0,this._error="";try{const e=await fetch(t);if(!e.ok)throw new Error(`HTTP ${e.status}`);this.offer=await e.json()}catch(e){this._error=e.message}finally{this._loading=!1}}_onCta(){this.dispatchEvent(new CustomEvent("od-cta-click",{detail:{offer:this.offer},bubbles:!0,composed:!0}))}_pin(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>`}_moon(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>`}_star(){return c`<svg viewBox="0 0 24 24" fill="currentColor" stroke="none" aria-hidden="true"><path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/></svg>`}_arrowR(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`}render(){if(this._loading)return c`<div class="status">Зарежда…</div>`;if(this._error)return c`<div class="status error">${this._error}</div>`;if(!this.offer)return l;const t=this.offer,e=t.nights??t.durationNights??0,i=t.img??t.imageUrl??"",r=A[t.board??t.boardBasis??""]??"",s=V[t.cat??""]??"",n=t.region??t.city??"",p=n?`${n}, ${t.country}`:t.country;return c`
      <article class="details" part="details">
        <div class="main">
          <div class="media">
            ${i?c`<img part="image" src=${i} alt=${t.title} />`:l}
            ${s?c`<span class="tag">${s}</span>`:l}
          </div>

          <div class="body" part="body">
            ${p?c`<span class="loc">${this._pin()}${p}</span>`:l}
            <h2 class="title" part="title">${t.title}</h2>

            <div class="meta" part="meta">
              ${e?c`<span class="chip">${this._moon()}${e} нощувки</span>`:l}
              ${r?c`<span class="chip">${r}</span>`:l}
              ${t.rating?c`<span class="chip" style="color:var(--odc-accent)">${this._star()}${t.rating.toFixed(1)}</span>`:l}
              ${t.transport?c`<span class="chip">${t.transport==="plane"?"Самолет":"Автобус"}</span>`:l}
            </div>

            ${t.description?c`<p class="desc">${t.description}</p>`:l}
          </div>
        </div>

        <aside class="sidebar">
          <div class="price-box">
            <div class="from">от</div>
            <div class="price" part="price">€${t.price}<small> / човек</small></div>
            <button class="cta" part="cta" @click=${this._onCta}>
              ${this.ctaLabel}${this._arrowR()}
            </button>
            <slot name="actions"></slot>
          </div>

          <slot name="booking-inquiry"></slot>
        </aside>
      </article>
    `}},a.OdOfferDetails.styles=x`
    :host {
      display: block;
      --odc-font:         system-ui, sans-serif;
      --odc-font-head:    system-ui, sans-serif;
      --odc-accent:       #1a5a61;
      --odc-accent-ink:   #ffffff;
      --odc-accent-soft:  #eef6f6;
      --odc-price:        #0e1618;
      --odc-ink:          #15201f;
      --odc-muted:        #5f6b68;
      --odc-bg:           #ffffff;
      --odc-surface:      #ffffff;
      --odc-border:       rgba(20,30,28,0.12);
      --odc-radius:       14px;
      --odc-radius-sm:    9px;
      --odc-shadow:       0 1px 2px rgba(16,24,22,0.06);
      --odc-tag-bg:       #15201f;
      --odc-tag-ink:      #ffffff;
    }
    *, *::before, *::after { box-sizing: border-box; }

    .details {
      display: grid;
      grid-template-columns: 1fr;
      gap: 24px;
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
    }
    @media (min-width: 720px) {
      .details { grid-template-columns: 1fr 340px; }
    }

    /* ── main column ── */
    .main { display: flex; flex-direction: column; gap: 20px; }

    .media {
      position: relative;
      aspect-ratio: 16 / 9;
      border-radius: var(--odc-radius);
      overflow: hidden;
      background: var(--odc-accent-soft);
    }
    .media img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
    .tag {
      position: absolute;
      top: 14px;
      left: 14px;
      background: var(--odc-tag-bg);
      color: var(--odc-tag-ink);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      padding: 5px 12px;
      border-radius: 999px;
    }

    .body { display: flex; flex-direction: column; gap: 14px; }
    .loc {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: 13px;
      color: var(--odc-muted);
      font-weight: 500;
    }
    .loc svg { width: 14px; height: 14px; flex: none; }
    h2.title {
      font-family: var(--odc-font-head);
      font-size: clamp(22px, 3vw, 30px);
      font-weight: 700;
      line-height: 1.2;
      margin: 0;
      color: var(--odc-ink);
    }
    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 13px;
      color: var(--odc-muted);
      background: var(--odc-accent-soft);
      border-radius: 999px;
      padding: 5px 12px;
    }
    .chip svg { width: 14px; height: 14px; flex: none; }
    .desc {
      font-size: 15px;
      line-height: 1.6;
      color: var(--odc-ink);
      margin: 0;
    }

    /* ── sidebar ── */
    .sidebar { display: flex; flex-direction: column; gap: 16px; }
    .price-box {
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius);
      padding: 20px;
      box-shadow: var(--odc-shadow);
    }
    .from { font-size: 12px; color: var(--odc-muted); margin-bottom: 2px; }
    .price {
      font-family: var(--odc-font-head);
      font-size: 32px;
      font-weight: 700;
      color: var(--odc-price);
      line-height: 1;
    }
    .price small { font-size: 14px; font-weight: 500; color: var(--odc-muted); }
    .cta {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      width: 100%;
      margin-top: 16px;
      height: 48px;
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border: 0;
      border-radius: var(--odc-radius-sm);
      font-family: var(--odc-font);
      font-size: 15px;
      font-weight: 600;
      cursor: pointer;
      transition: filter 0.15s ease;
    }
    .cta:hover { filter: brightness(1.06); }
    .cta svg { width: 16px; height: 16px; flex: none; }

    /* ── status ── */
    .status {
      padding: 32px;
      text-align: center;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 14px;
    }
    .status.error { color: #b3261e; }
  `,j([d({type:Object})],a.OdOfferDetails.prototype,"offer",2),j([d({attribute:"offer-id"})],a.OdOfferDetails.prototype,"offerId",2),j([d({attribute:"api-base"})],a.OdOfferDetails.prototype,"apiBase",2),j([d({attribute:"cta-label"})],a.OdOfferDetails.prototype,"ctaLabel",2),j([f()],a.OdOfferDetails.prototype,"_loading",2),j([f()],a.OdOfferDetails.prototype,"_error",2),a.OdOfferDetails=j([w("od-offer-details")],a.OdOfferDetails);var de=Object.defineProperty,le=Object.getOwnPropertyDescriptor,W=(o,t,e,i)=>{for(var r=i>1?void 0:i?le(t,e):t,s=o.length-1,n;s>=0;s--)(n=o[s])&&(r=(i?n(t,e,r):n(r))||r);return i&&r&&de(t,e,r),r};a.OdPriceTable=class extends y{constructor(){super(...arguments),this.currency="€"}_demoRows(t){return[{period:"02 юни – 09 юни",board:"Закуска",price:t,best:!1},{period:"16 юни – 23 юни",board:"Закуска",price:t+80,best:!1},{period:"30 юни – 07 юли",board:"Полупансион",price:t+140,best:!0},{period:"14 юли – 21 юли",board:"Полупансион",price:t+210,best:!1}]}render(){const t=this.rows??(this.offerPrice!=null?this._demoRows(this.offerPrice):[]);if(t.length===0)return c`<div class="empty">Няма налични дати.</div>`;const e=t.some(i=>i.occupancy);return c`
      <div class="wrap">
        <table part="table">
          ${this.caption?c`<caption>${this.caption}</caption>`:l}
          <thead>
            <tr part="head">
              <th>Период</th>
              <th>Изхранване</th>
              ${e?c`<th>Заетост</th>`:l}
              <th style="text-align:right">Цена / човек</th>
            </tr>
          </thead>
          <tbody>
            ${t.map(i=>c`
              <tr class=${i.best?"best":""} part="${i.best?"row best":"row"}">
                <td>${i.period}${i.best?c`<span class="badge">Топ</span>`:l}</td>
                <td>${i.board}</td>
                ${e?c`<td>${i.occupancy??"—"}</td>`:l}
                <td class="price" part="price">${i.currency??this.currency}${i.price}</td>
              </tr>
            `)}
          </tbody>
        </table>
      </div>
    `}},a.OdPriceTable.styles=x`
    :host {
      display: block;
      --odc-font:        system-ui, sans-serif;
      --odc-font-head:   system-ui, sans-serif;
      --odc-accent:      #1a5a61;
      --odc-accent-soft: #eef6f6;
      --odc-price:       #0e1618;
      --odc-ink:         #15201f;
      --odc-muted:       #5f6b68;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
    }
    *, *::before, *::after { box-sizing: border-box; }

    .wrap {
      overflow-x: auto;
      border-radius: var(--odc-radius);
      border: 1px solid var(--odc-border);
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13.5px;
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
      min-width: 360px;
    }
    caption {
      font-family: var(--odc-font-head);
      font-size: 15px;
      font-weight: 700;
      text-align: left;
      padding: 12px 16px 8px;
      color: var(--odc-ink);
      caption-side: top;
    }
    th {
      background: var(--odc-accent-soft);
      color: var(--odc-ink);
      font-weight: 700;
      text-align: left;
      padding: 10px 16px;
      font-size: 12px;
      letter-spacing: 0.03em;
      text-transform: uppercase;
      white-space: nowrap;
    }
    td {
      padding: 11px 16px;
      border-top: 1px solid var(--odc-border);
      color: var(--odc-ink);
      white-space: nowrap;
    }
    td.price {
      font-weight: 700;
      color: var(--odc-price);
      text-align: right;
      font-family: var(--odc-font-head);
      font-size: 15px;
    }
    tr.best td { background: var(--odc-accent-soft); }
    tr.best td.price { color: var(--odc-accent); }
    .badge {
      display: inline-block;
      background: var(--odc-accent);
      color: var(--odc-accent-ink, #fff);
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      padding: 2px 7px;
      border-radius: 999px;
      margin-left: 8px;
      vertical-align: middle;
    }
    .empty {
      padding: 24px;
      text-align: center;
      color: var(--odc-muted);
      font-size: 14px;
    }
  `,W([d({type:Array})],a.OdPriceTable.prototype,"rows",2),W([d({attribute:"offer-price",type:Number})],a.OdPriceTable.prototype,"offerPrice",2),W([d({type:String})],a.OdPriceTable.prototype,"currency",2),W([d({type:String})],a.OdPriceTable.prototype,"caption",2),a.OdPriceTable=W([w("od-price-table")],a.OdPriceTable);var pe=Object.defineProperty,he=Object.getOwnPropertyDescriptor,_=(o,t,e,i)=>{for(var r=i>1?void 0:i?he(t,e):t,s=o.length-1,n;s>=0;s--)(n=o[s])&&(r=(i?n(t,e,r):n(r))||r);return i&&r&&pe(t,e,r),r};a.OdBookingInquiry=class extends y{constructor(){super(...arguments),this.offerTitle="",this.offerId="",this.heading="Запитване за оферта",this.submitLabel="Изпрати запитване",this.publicationKey="",this.channel="WebComponent",this._submitted=!1,this._submitting=!1,this._startSent=!1,this._name="",this._phone="",this._email="",this._message=""}_onFocusIn(){this._startSent||(this._startSent=!0,this.dispatchEvent(new CustomEvent("od-inquiry-start",{detail:{offerId:this.offerId||void 0,publicationKey:this.publicationKey},bubbles:!0,composed:!0})),Q({eventType:"InquiryStart",publicationKey:this.publicationKey,offerId:this.offerId||void 0,channel:this.channel}))}async _submit(t){if(t.preventDefault(),this._submitting)return;this._submitting=!0;const e={name:this._name.trim(),phone:this._phone.trim(),email:this._email.trim(),message:this._message.trim(),offerId:this.offerId||void 0,offerTitle:this.offerTitle||void 0};this.dispatchEvent(new CustomEvent("od-inquiry-submit",{detail:e,bubbles:!0,composed:!0})),Q({eventType:"InquirySubmit",publicationKey:this.publicationKey,offerId:this.offerId||void 0,channel:this.channel}),await new Promise(i=>setTimeout(i,400)),this._submitted=!0,this._submitting=!1}_arrowR(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`}_check(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>`}render(){return this._submitted?c`
        <div class="success" part="success-msg">
          ${this._check()}
          <strong>Запитването е изпратено!</strong>
          <p>Ще се свържем с вас до 24 часа.</p>
        </div>
      `:c`
      <form part="form" @submit=${this._submit} @focusin=${this._onFocusIn} novalidate>
        <h4 part="heading">${this.heading}</h4>
        ${this.offerTitle?c`<p class="sub">${this.offerTitle} · отговаряме до 24 часа</p>`:l}

        <div class="row">
          <input
            part="field"
            type="text"
            placeholder="Име и фамилия *"
            required
            autocomplete="name"
            .value=${this._name}
            @input=${t=>{this._name=t.target.value}}
          />
          <input
            part="field"
            type="tel"
            placeholder="Телефон *"
            required
            autocomplete="tel"
            .value=${this._phone}
            @input=${t=>{this._phone=t.target.value}}
          />
        </div>

        <input
          part="field"
          type="email"
          placeholder="Имейл *"
          required
          autocomplete="email"
          .value=${this._email}
          @input=${t=>{this._email=t.target.value}}
        />

        <textarea
          part="field"
          placeholder="Брой пътници, период, въпроси…"
          .value=${this._message}
          @input=${t=>{this._message=t.target.value}}
        ></textarea>

        <p class="required-note">* Задължителни полета</p>

        <button
          class="submit-btn"
          part="submit"
          type="submit"
          ?disabled=${this._submitting||!this._name||!this._phone||!this._email}
        >
          ${this._submitting?"Изпраща…":c`${this.submitLabel}${this._arrowR()}`}
        </button>
      </form>
    `}},a.OdBookingInquiry.styles=x`
    :host {
      display: block;
      --odc-font:        system-ui, sans-serif;
      --odc-font-head:   system-ui, sans-serif;
      --odc-accent:      #1a5a61;
      --odc-accent-ink:  #ffffff;
      --odc-accent-soft: #eef6f6;
      --odc-ink:         #15201f;
      --odc-muted:       #5f6b68;
      --odc-bg:          #ffffff;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
      --odc-radius-sm:   9px;
      --odc-shadow:      0 1px 2px rgba(16,24,22,0.06);
    }
    *, *::before, *::after { box-sizing: border-box; }

    form {
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius);
      padding: 20px;
      box-shadow: var(--odc-shadow);
      font-family: var(--odc-font);
      color: var(--odc-ink);
      -webkit-font-smoothing: antialiased;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    h4 {
      font-family: var(--odc-font-head);
      font-size: 18px;
      font-weight: 700;
      margin: 0;
      color: var(--odc-ink);
    }
    .sub {
      font-size: 13px;
      color: var(--odc-muted);
      margin: 0;
    }
    .row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px;
    }
    @media (max-width: 480px) { .row { grid-template-columns: 1fr; } }

    input, textarea {
      width: 100%;
      padding: 0 12px;
      height: 42px;
      border: 1px solid var(--odc-border);
      border-radius: var(--odc-radius-sm);
      font-family: var(--odc-font);
      font-size: 13.5px;
      color: var(--odc-ink);
      background: var(--odc-bg);
      outline: none;
      transition: border-color 0.15s ease;
    }
    input:focus, textarea:focus { border-color: var(--odc-accent); }
    textarea { height: 80px; padding: 10px 12px; resize: vertical; }

    .required-note {
      font-size: 12px;
      color: var(--odc-muted);
      margin: 0;
    }
    .submit-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      height: 46px;
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border: 0;
      border-radius: var(--odc-radius-sm);
      font-family: var(--odc-font);
      font-size: 14.5px;
      font-weight: 600;
      cursor: pointer;
      transition: filter 0.15s ease;
      width: 100%;
    }
    .submit-btn:hover { filter: brightness(1.06); }
    .submit-btn:disabled { opacity: 0.6; cursor: not-allowed; }
    .submit-btn svg { width: 16px; height: 16px; flex: none; }

    .success {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 10px;
      padding: 24px;
      text-align: center;
    }
    .success svg { width: 40px; height: 40px; color: var(--odc-accent); }
    .success strong { font-size: 16px; font-weight: 700; color: var(--odc-ink); }
    .success p { font-size: 13px; color: var(--odc-muted); margin: 0; }
  `,_([d({attribute:"offer-title"})],a.OdBookingInquiry.prototype,"offerTitle",2),_([d({attribute:"offer-id"})],a.OdBookingInquiry.prototype,"offerId",2),_([d({type:String})],a.OdBookingInquiry.prototype,"heading",2),_([d({attribute:"submit-label"})],a.OdBookingInquiry.prototype,"submitLabel",2),_([d({attribute:"publication-key"})],a.OdBookingInquiry.prototype,"publicationKey",2),_([d({attribute:"channel"})],a.OdBookingInquiry.prototype,"channel",2),_([f()],a.OdBookingInquiry.prototype,"_submitted",2),_([f()],a.OdBookingInquiry.prototype,"_submitting",2),_([f()],a.OdBookingInquiry.prototype,"_name",2),_([f()],a.OdBookingInquiry.prototype,"_phone",2),_([f()],a.OdBookingInquiry.prototype,"_email",2),_([f()],a.OdBookingInquiry.prototype,"_message",2),a.OdBookingInquiry=_([w("od-booking-inquiry")],a.OdBookingInquiry);var fe=Object.defineProperty,ue=Object.getOwnPropertyDescriptor,I=(o,t,e,i)=>{for(var r=i>1?void 0:i?ue(t,e):t,s=o.length-1,n;s>=0;s--)(n=o[s])&&(r=(i?n(t,e,r):n(r))||r);return i&&r&&fe(t,e,r),r};a.OdDestinationHero=class extends y{constructor(){super(...arguments),this.image="",this.eyebrow="",this.title="",this.subtitle="",this.ctaLabel="",this.minHeight="230px"}updated(t){t.has("minHeight")&&this.style.setProperty("--_min-h",this.minHeight)}_onCta(){this.dispatchEvent(new CustomEvent("od-hero-cta",{bubbles:!0,composed:!0}))}_arrowR(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`}render(){return c`
      <div class="hero" part="hero">
        ${this.image?c`<img class="hero-img" src=${this.image} alt=${this.title} />`:l}
        <div class="inner" part="inner">
          ${this.eyebrow?c`<span class="eyebrow" part="eyebrow">${this.eyebrow}</span>`:l}
          ${this.title?c`<h2 class="title" part="title">${this.title}</h2>`:l}
          ${this.subtitle?c`<p class="subtitle" part="subtitle">${this.subtitle}</p>`:l}
          ${this.ctaLabel?c`
            <button class="cta" part="cta" @click=${this._onCta}>
              ${this.ctaLabel}${this._arrowR()}
            </button>`:l}
          <slot></slot>
        </div>
      </div>
    `}},a.OdDestinationHero.styles=x`
    :host {
      display: block;
      --odc-font:       system-ui, sans-serif;
      --odc-font-head:  system-ui, sans-serif;
      --odc-accent:     #1a5a61;
      --odc-accent-ink: #ffffff;
      --odc-radius:     14px;
    }
    *, *::before, *::after { box-sizing: border-box; }

    .hero {
      position: relative;
      border-radius: var(--odc-radius);
      overflow: hidden;
      min-height: var(--_min-h, 230px);
      display: flex;
      align-items: flex-end;
      padding: 28px 28px 28px;
      color: #fff;
      -webkit-font-smoothing: antialiased;
    }
    .hero-img {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
    /* gradient scrim — bottom-heavy for legibility */
    .hero::after {
      content: "";
      position: absolute;
      inset: 0;
      background: linear-gradient(0deg, rgba(0,0,0,0.68) 0%, rgba(0,0,0,0.05) 65%);
      pointer-events: none;
    }
    .inner {
      position: relative;
      z-index: 1;
      max-width: 600px;
    }
    .eyebrow {
      font-family: var(--odc-font);
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      opacity: 0.9;
      display: block;
      margin-bottom: 8px;
    }
    h2.title {
      font-family: var(--odc-font-head);
      font-size: clamp(24px, 3.5vw, 36px);
      font-weight: 700;
      margin: 0 0 8px;
      line-height: 1.1;
    }
    p.subtitle {
      font-family: var(--odc-font);
      font-size: 15px;
      opacity: 0.92;
      max-width: 480px;
      margin: 0 0 16px;
      line-height: 1.5;
    }
    .cta {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      height: 44px;
      padding: 0 20px;
      background: var(--odc-accent);
      color: var(--odc-accent-ink);
      border: 0;
      border-radius: 9px;
      font-family: var(--odc-font);
      font-size: 14px;
      font-weight: 600;
      cursor: pointer;
      transition: filter 0.15s ease;
    }
    .cta:hover { filter: brightness(1.1); }
    .cta svg { width: 16px; height: 16px; flex: none; }
  `,I([d({type:String})],a.OdDestinationHero.prototype,"image",2),I([d({type:String})],a.OdDestinationHero.prototype,"eyebrow",2),I([d({type:String})],a.OdDestinationHero.prototype,"title",2),I([d({type:String})],a.OdDestinationHero.prototype,"subtitle",2),I([d({attribute:"cta-label"})],a.OdDestinationHero.prototype,"ctaLabel",2),I([d({attribute:"min-height"})],a.OdDestinationHero.prototype,"minHeight",2),a.OdDestinationHero=I([w("od-destination-hero")],a.OdDestinationHero);var ge=Object.defineProperty,be=Object.getOwnPropertyDescriptor,k=(o,t,e,i)=>{for(var r=i>1?void 0:i?be(t,e):t,s=o.length-1,n;s>=0;s--)(n=o[s])&&(r=(i?n(t,e,r):n(r))||r);return i&&r&&ge(t,e,r),r};a.OdFeaturedCollections=class extends y{constructor(){super(...arguments),this.apiBase="",this.columns=3,this._fetched=[],this._loading=!1,this._error=""}get _url(){return this.endpoint?this.endpoint:this.apiBase?`${this.apiBase}/api/v1/collections`:""}connectedCallback(){super.connectedCallback(),!this.collections&&this._url&&this._load()}updated(t){["endpoint","apiBase"].some(e=>t.has(e))&&!this.collections&&this._load(),t.has("columns")&&this.style.setProperty("--_cols",`repeat(${this.columns}, minmax(0, 1fr))`)}async _load(){const t=this._url;if(t){this._loading=!0,this._error="";try{const e=await fetch(t);if(!e.ok)throw new Error(`HTTP ${e.status}`);this._fetched=await e.json()}catch(e){this._error=e.message}finally{this._loading=!1}}}_onClick(t,e){t.preventDefault(),this.dispatchEvent(new CustomEvent("od-collection-click",{detail:{collection:e},bubbles:!0,composed:!0}))}_gridStyle(){return this.columns?`grid-template-columns: repeat(${this.columns}, minmax(0, 1fr))`:""}_gridIcon(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>`}_arrowR(){return c`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M12 5l7 7-7 7"/></svg>`}render(){const t=this.collections??this._fetched;return this._loading&&t.length===0?c`
        ${this.title?c`<div class="head" part="head"><h2 class="head-title">${this.title}</h2></div>`:l}
        <div class="grid" style=${this._gridStyle()} part="grid">
          ${Array.from({length:this.columns},()=>c`<div class="skel"></div>`)}
        </div>
      `:this._error?c`<div class="status error">${this._error}</div>`:t.length===0?c`<div class="status">Няма колекции.</div>`:c`
      ${this.title?c`
        <div class="head" part="head">
          <h2 class="head-title">${this.title}</h2>
        </div>`:l}
      <div class="grid" style=${this._gridStyle()} part="grid">
        ${t.map(e=>c`
          <a
            class="card"
            part="card"
            href="#"
            role="button"
            aria-label=${e.title}
            @click=${i=>this._onClick(i,e)}
          >
            <div class="card-media">
              ${e.imageUrl?c`<img part="image" src=${e.imageUrl} alt=${e.title} loading="lazy" />`:c`<div class="card-placeholder">${this._gridIcon()}</div>`}
            </div>
            <div class="card-body">
              <span class="label" part="label">${e.title}</span>
              ${e.description?c`<p class="desc">${e.description}</p>`:l}
              <div class="card-foot">
                ${e.count!=null?c`<span class="count" part="count">${e.count} оферти</span>`:c`<span></span>`}
                <span class="arrow">${this._arrowR()}</span>
              </div>
            </div>
          </a>
        `)}
      </div>
    `}},a.OdFeaturedCollections.styles=x`
    :host {
      display: block;
      --odc-font:        system-ui, sans-serif;
      --odc-font-head:   system-ui, sans-serif;
      --odc-accent:      #1a5a61;
      --odc-accent-ink:  #ffffff;
      --odc-accent-soft: #eef6f6;
      --odc-ink:         #15201f;
      --odc-muted:       #5f6b68;
      --odc-surface:     #ffffff;
      --odc-border:      rgba(20,30,28,0.12);
      --odc-radius:      14px;
      --odc-shadow:      0 1px 2px rgba(16,24,22,0.06);
      --odc-shadow-hover:0 12px 30px rgba(16,24,22,0.14);
    }
    *, *::before, *::after { box-sizing: border-box; }

    .head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      margin-bottom: 16px;
      gap: 12px;
      font-family: var(--odc-font);
    }
    .head-title {
      font-family: var(--odc-font-head);
      font-size: 22px;
      font-weight: 700;
      margin: 0;
      color: var(--odc-ink);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 16px;
    }

    /* collection card */
    .card {
      display: flex;
      flex-direction: column;
      border-radius: var(--odc-radius);
      overflow: hidden;
      background: var(--odc-surface);
      border: 1px solid var(--odc-border);
      box-shadow: var(--odc-shadow);
      cursor: pointer;
      text-decoration: none;
      color: inherit;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
    }
    .card:hover {
      transform: translateY(-3px);
      box-shadow: var(--odc-shadow-hover);
    }
    .card-media {
      position: relative;
      aspect-ratio: 3 / 2;
      overflow: hidden;
      background: var(--odc-accent-soft);
      flex: none;
    }
    .card-media img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      transition: transform 0.5s ease;
      display: block;
    }
    .card:hover .card-media img { transform: scale(1.05); }
    .card-placeholder {
      width: 100%;
      height: 100%;
      background: linear-gradient(135deg, var(--odc-accent-soft), var(--odc-border));
      display: grid;
      place-items: center;
    }
    .card-placeholder svg { width: 32px; height: 32px; color: var(--odc-accent); opacity: 0.5; }

    .card-body {
      padding: 12px 14px 14px;
      display: flex;
      flex-direction: column;
      gap: 4px;
      flex: 1;
    }
    .label {
      font-family: var(--odc-font-head);
      font-size: 15px;
      font-weight: 700;
      color: var(--odc-ink);
      line-height: 1.3;
    }
    .desc {
      font-size: 12.5px;
      color: var(--odc-muted);
      line-height: 1.4;
      margin: 0;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .card-foot {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: auto;
      padding-top: 10px;
    }
    .count {
      font-size: 12px;
      color: var(--odc-muted);
    }
    .arrow {
      width: 24px;
      height: 24px;
      display: grid;
      place-items: center;
      border-radius: 999px;
      background: var(--odc-accent-soft);
      color: var(--odc-accent);
      transition: background 0.15s ease;
      flex: none;
    }
    .card:hover .arrow { background: var(--odc-accent); color: var(--odc-accent-ink); }
    .arrow svg { width: 13px; height: 13px; }

    /* status */
    .status {
      padding: 32px;
      text-align: center;
      color: var(--odc-muted);
      font-family: var(--odc-font);
      font-size: 14px;
    }
    .status.error { color: #b3261e; }
    .skel {
      height: 200px;
      background: var(--odc-accent-soft);
      border-radius: var(--odc-radius);
      animation: pulse 1.4s ease-in-out infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50%       { opacity: 0.4; }
    }
  `,k([d({type:String})],a.OdFeaturedCollections.prototype,"endpoint",2),k([d({attribute:"api-base"})],a.OdFeaturedCollections.prototype,"apiBase",2),k([d({type:String})],a.OdFeaturedCollections.prototype,"title",2),k([d({type:Number})],a.OdFeaturedCollections.prototype,"columns",2),k([d({type:Array})],a.OdFeaturedCollections.prototype,"collections",2),k([f()],a.OdFeaturedCollections.prototype,"_fetched",2),k([f()],a.OdFeaturedCollections.prototype,"_loading",2),k([f()],a.OdFeaturedCollections.prototype,"_error",2),a.OdFeaturedCollections=k([w("od-featured-collections")],a.OdFeaturedCollections),a.BOARD_LABELS=A,a.CAT_LABELS=V,a.sendOdEvent=Q,Object.defineProperty(a,Symbol.toStringTag,{value:"Module"})});
//# sourceMappingURL=odisea-components.umd.js.map
